using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FotoFromFaceControl.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectFaceFeaturesController : ControllerBase
    {
        private readonly CascadeClassifier _faceCascade;
        private readonly CascadeClassifier _eyeCascade;
        private readonly CascadeClassifier _noseCascade;
        private readonly IWebHostEnvironment _env;
        private readonly Channel<Func<CancellationToken, Task>> _queue;

        public DetectFaceFeaturesController(Channel<Func<CancellationToken, Task>> queue, IWebHostEnvironment env)
        {
            _queue = queue;
            _env = env;

            string cascadePath = Path.Combine(env.WebRootPath, "Cascades");

            _faceCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_frontalface_default.xml"));
            _eyeCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_eye.xml"));
            _noseCascade = new CascadeClassifier(Path.Combine(cascadePath, "nose.xml"));
        }

        [HttpPost("upload-and-process")]
        public async Task<IActionResult> UploadAndProcessImage([FromForm] FileUploadModel file)
        {
            if (file == null || file.File.Length == 0)
                return BadRequest("Dosya yüklenmedi.");

            var jobId = Guid.NewGuid().ToString();
            string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");
            string tempFolder = Path.Combine(_env.WebRootPath, "TempResults", jobId);
            string resultsFolder = Path.Combine(_env.WebRootPath, "Results");
            string zipPath = Path.Combine(resultsFolder, $"{jobId}.zip");

            Directory.CreateDirectory(uploadsFolder);
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(resultsFolder);

            string inputPath = Path.Combine(uploadsFolder, $"{jobId}{Path.GetExtension(file.File.FileName)}");

            // Resmi kaydet
            using (var fs = new FileStream(inputPath, FileMode.Create))
            {
                await file.File.CopyToAsync(fs);
            }

            try
            {
                using var img = new Image<Bgr, byte>(inputPath);
                using var gray = img.Convert<Gray, byte>();

                var faces = _faceCascade.DetectMultiScale(gray, 1.05, 4, new Size(30, 30));
                foreach (var face in faces)
                {
                    CvInvoke.Rectangle(img, face, new Bgr(Color.Red).MCvScalar, 2);
                    var faceROI = new Mat(gray.Mat, face);

                    var eyes = _eyeCascade.DetectMultiScale(faceROI, 1.05, 3, new Size(15, 15));
                    foreach (var eye in eyes)
                    {
                        var eyeRect = new Rectangle(eye.X + face.X, eye.Y + face.Y, eye.Width, eye.Height);
                        CvInvoke.Rectangle(img, eyeRect, new Bgr(Color.Green).MCvScalar, 2);
                    }

                    var noses = _noseCascade.DetectMultiScale(faceROI, 1.05, 3, new Size(15, 15));
                    foreach (var nose in noses)
                    {
                        var noseRect = new Rectangle(nose.X + face.X, nose.Y + face.Y, nose.Width, nose.Height);
                        CvInvoke.Rectangle(img, noseRect, new Bgr(Color.Blue).MCvScalar, 2);
                    }
                }

                string outputPath = Path.Combine(tempFolder, "result.jpg");
                img.ToBitmap().Save(outputPath, System.Drawing.Imaging.ImageFormat.Jpeg);

                // Zip oluştur - geçici zip dosyası oluştur ve tamamlanınca asıl dosya adı ile değiştir
                string tempZipPath = zipPath + ".tmp";
                if (System.IO.File.Exists(tempZipPath))
                    System.IO.File.Delete(tempZipPath);

                ZipFile.CreateFromDirectory(tempFolder, tempZipPath);

                if (System.IO.File.Exists(zipPath))
                    System.IO.File.Delete(zipPath);

                System.IO.File.Move(tempZipPath, zipPath);

                Directory.Delete(tempFolder, true);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata oluştu: {ex.Message}");
            }

            return Ok(new
            {
                jobId,
                message = "Resim işleme tamamlandı. Sonucu GET /api/DetectFaceFeatures/result/{jobId} ile alabilirsiniz."
            });
        }


        [HttpPost("upload-video")]
        public async Task<IActionResult> UploadAndProcessVideo([FromForm] FileUploadModel file, [FromForm] bool cropFace = true)
        {
            if (file == null || file.File.Length == 0)
                return BadRequest("Dosya yüklenmedi.");

            var jobId = Guid.NewGuid().ToString();

            string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");
            string tempFolder = Path.Combine(_env.WebRootPath, "TempResults", jobId);
            string resultsFolder = Path.Combine(_env.WebRootPath, "Results");
            string zipPath = Path.Combine(resultsFolder, $"{jobId}.zip");

            Directory.CreateDirectory(uploadsFolder);
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(resultsFolder);

            string inputPath = Path.Combine(uploadsFolder, $"{jobId}{Path.GetExtension(file.File.FileName)}");

            // Videoyu kaydet
            using (var fs = new FileStream(inputPath, FileMode.Create))
            {
                await file.File.CopyToAsync(fs);
            }

            // Kuyruğa ekle
            bool yazildi = _queue.Writer.TryWrite(async token =>
            {
                try
                {
                    using var capture = new VideoCapture(inputPath);
                    int frameIndex = 0;
                    Mat frame = new Mat();

                    ZipArchive? archive = null;
                    FileStream? zipStream = null;

                    if (!cropFace)
                    {
                        zipStream = new FileStream(zipPath + ".tmp", FileMode.Create);
                        archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
                    }

                    while (true)
                    {
                        if (!capture.Read(frame) || frame.IsEmpty)
                            break;

                        using var image = frame.ToImage<Bgr, byte>();
                        using var gray = image.Convert<Gray, byte>();

                        var faces = _faceCascade.DetectMultiScale(gray, 1.05, 3, new Size(30, 30));

                        if (cropFace)
                        {
                            // İşaretleme yap (rectangle çiz)
                            var imgWithRects = image.Clone();

                            foreach (var face in faces)
                            {
                                CvInvoke.Rectangle(imgWithRects, face, new Bgr(Color.Red).MCvScalar, 2);

                                var faceROI = new Mat(gray.Mat, face);

                                var eyes = _eyeCascade.DetectMultiScale(faceROI, 1.05, 3, new Size(15, 15));
                                foreach (var eye in eyes)
                                {
                                    var eyeRect = new Rectangle(eye.X + face.X, eye.Y + face.Y, eye.Width, eye.Height);
                                    CvInvoke.Rectangle(imgWithRects, eyeRect, new Bgr(Color.Green).MCvScalar, 2);
                                }

                                var noses = _noseCascade.DetectMultiScale(faceROI, 1.05, 3, new Size(15, 15));
                                foreach (var nose in noses)
                                {
                                    var noseRect = new Rectangle(nose.X + face.X, nose.Y + face.Y, nose.Width, nose.Height);
                                    CvInvoke.Rectangle(imgWithRects, noseRect, new Bgr(Color.Blue).MCvScalar, 2);
                                }
                            }

                            using var bmp = imgWithRects.ToBitmap();
                            string fileName = $"frame_{frameIndex}.jpg";
                            string savePath = Path.Combine(tempFolder, fileName);
                            bmp.Save(savePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                        else
                        {
                            // Crop yap ve zip içine ekle
                            int faceCount = 0;
                            foreach (var face in faces)
                            {
                                using var croppedFace = image.Copy(face);
                                using var bmp = croppedFace.ToBitmap();

                                var entry = archive.CreateEntry($"frame_{frameIndex}_face_{faceCount}.jpg");
                                using var entryStream = entry.Open();
                                bmp.Save(entryStream, System.Drawing.Imaging.ImageFormat.Jpeg);

                                faceCount++;
                            }
                        }

                        frameIndex++;
                    }

                    if (!cropFace)
                    {
                        archive?.Dispose();
                        zipStream?.Dispose();

                        // Zip oluşturma tamamlandıktan sonra geçici dosyayı asıl zip dosyasına taşı
                        string tempZipPath = zipPath + ".tmp";
                        if (System.IO.File.Exists(zipPath))
                            System.IO.File.Delete(zipPath);
                        System.IO.File.Move(tempZipPath, zipPath);
                    }
                    else
                    {
                        // İşaretleme sonucu resimleri ziple
                        if (Directory.Exists(tempFolder))
                        {
                            string tempZipPath = zipPath + ".tmp";
                            if (System.IO.File.Exists(tempZipPath))
                                System.IO.File.Delete(tempZipPath);

                            ZipFile.CreateFromDirectory(tempFolder, tempZipPath);

                            if (System.IO.File.Exists(zipPath))
                                System.IO.File.Delete(zipPath);

                            System.IO.File.Move(tempZipPath, zipPath);

                            Directory.Delete(tempFolder, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Video işleme hatası: {ex}");
                }
            });

            if (!yazildi)
                return StatusCode(503, "İşlem kuyruğa alınamadı, lütfen tekrar deneyin.");

            return Ok(new
            {
                jobId,
                message = "Video kuyruğa alındı. Sonucu GET /api/DetectFaceFeatures/result/{jobId} ile sorgulayabilirsiniz."
            });
        }

        [HttpGet("result/{jobId}")]
        public IActionResult GetResult(string jobId)
        {
            string zipPath = Path.Combine(_env.WebRootPath, "Results", $"{jobId}.zip");

            if (!System.IO.File.Exists(zipPath))
                return StatusCode(202, new { status = "pending" });

            FileStream stream = null;
            int retries = 5;
            int delay = 200;

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    break;
                }
                catch (IOException)
                {
                    if (i == retries - 1)
                        throw;
                    Thread.Sleep(delay);
                }
            }

            return File(stream, "application/zip", $"{jobId}.zip");
        }

        public class FileUploadModel
        {
            public Microsoft.AspNetCore.Http.IFormFile File { get; set; }
        }
    }
}


