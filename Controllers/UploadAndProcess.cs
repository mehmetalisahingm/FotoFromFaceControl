using Emgu.CV;
using Emgu.CV.Structure;
using FotoFromFaceControl.Models;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.IO.Compression;

namespace FotoFromFaceControl.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DetectFaceFeaturesController : ControllerBase
    {
        private readonly CascadeClassifier _faceCascade;
        private readonly IWebHostEnvironment _env;

        public DetectFaceFeaturesController(IWebHostEnvironment env)
        {
            _env = env;
            string cascadePath = Path.Combine(env.WebRootPath, "CasCades");
            _faceCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_frontalface_default.xml"));
        }
        [HttpPost("upload-and-process")]
        public async Task<IActionResult> UploadAndProcessAsync(
            [FromForm] FileUploadModel file,
            [FromQuery] bool cropFace = false,
            [FromQuery] bool cropEyes = false,
            [FromQuery] bool cropNose = false,
            [FromQuery] float scaleFactor = 1.05f,
            [FromQuery] int minNeighbors = 3,
            [FromQuery] int minWidth = 30,
            [FromQuery] int minHeight = 30,
            [FromQuery] float distance = 0f)
        {
            if (file == null || file.File.Length == 0)
                return BadRequest("Dosya yüklenmedi.");

            try
            {
                await using var ms = new MemoryStream();
                await file.File.CopyToAsync(ms);
                ms.Position = 0;

                using var bitmap = new Bitmap(ms);
                using var img = bitmap.ToImage<Bgr, byte>();
                using var gray = img.Convert<Gray, byte>();

                var faces = _faceCascade.DetectMultiScale(gray, scaleFactor, minNeighbors, new Size(minWidth, minHeight));

                if (faces.Length == 0)
                    return BadRequest("Yüz bulunamadı.");

                var archiveStream = new MemoryStream();
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                {
                    int count = 1;
                    foreach (var face in faces)
                    {
                        var faceROI = new Rectangle(face.X, face.Y, face.Width, face.Height);
                        using var faceGray = new Mat(gray.Mat, faceROI);

                        if (cropFace)
                        {
                            using var croppedFace = img.Copy(face);
                            using var croppedBitmap = croppedFace.ToBitmap();
                            var entry = archive.CreateEntry($"face_{count}.jpg");
                            await using var entryStream = entry.Open();
                            croppedBitmap.Save(entryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }

                        // Aynı şekilde cropEyes ve cropNose bölümleri için de await eklenebilir.
                        // ...
                        count++;
                    }
                }

                archiveStream.Position = 0;
                return File(archiveStream.ToArray(), "application/zip", "selected_features.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata oluştu: {ex.Message}");
            }
        }


        // Video dosyası için yeni endpoint: kare kare işleme
        [HttpPost("upload-video")]
        public IActionResult UploadAndProcessVideo(
            [FromForm] FileUploadModel file,
            [FromQuery] float scaleFactor = 1.05f,
            [FromQuery] int minNeighbors = 3,
            [FromQuery] int minWidth = 30,
            [FromQuery] int minHeight = 30)
        {
            if (file == null || file.File.Length == 0)
                return BadRequest("Dosya yüklenmedi.");

            try
            {
                // Geçici klasör ve dosya yolu
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                
                var videoPath = Path.Combine(tempDir, file.File.FileName);

                using (var fs = new FileStream(videoPath, FileMode.Create))
                {
                    file.File.CopyTo(fs);
                }


                using (var fs = new FileStream(videoPath, FileMode.Create))
                {
                    file.File.CopyTo(fs);
                }

                var archiveStream = new MemoryStream();
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
                using (var capture = new VideoCapture(videoPath))
                {
                    int frameIndex = 0;
                    Mat frame = new Mat();

                    while (true)
                    {
                        if (!capture.Read(frame) || frame.IsEmpty)
                            break;

                        using var image = frame.ToImage<Bgr, byte>();
                        using var gray = image.Convert<Gray, byte>();

                        var faces = _faceCascade.DetectMultiScale(gray, scaleFactor, minNeighbors, new Size(minWidth, minHeight));

                        int faceCount = 0;
                        foreach (var face in faces)
                        {
                            using var croppedFace = image.Copy(face);
                            using var croppedBitmap = croppedFace.ToBitmap();

                            var entry = archive.CreateEntry($"frame_{frameIndex}_face_{faceCount}.jpg");
                            using var entryStream = entry.Open();
                            croppedBitmap.Save(entryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                            faceCount++;
                        }

                        frameIndex++;
                    }
                }

                archiveStream.Position = 0;
                Directory.Delete(tempDir, true);

                return File(archiveStream.ToArray(), "application/zip", "detected_faces_from_video.zip");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata oluştu: {ex.Message}");
            }
        }
    }
}
