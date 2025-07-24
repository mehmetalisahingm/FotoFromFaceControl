//using Emgu.CV;
//using Emgu.CV.Structure;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Drawing;
//using System.IO;
//using System.IO.Compression;
//using System.Threading;
//using System.Threading.Channels;
//using System.Threading.Tasks;

//namespace FotoFromFaceControl.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class SelectedFeaturesController : ControllerBase
//    {
//        private readonly CascadeClassifier _faceCascade;
//        private readonly CascadeClassifier _eyeCascade;
//        private readonly CascadeClassifier _noseCascade;
//        private readonly IWebHostEnvironment _env;
//        private readonly Channel<Func<CancellationToken, Task>> _queue;

//        public SelectedFeaturesController(Channel<Func<CancellationToken, Task>> queue, IWebHostEnvironment env)
//        {
//            _queue = queue;
//            _env = env;

//            string cascadePath = Path.Combine(env.WebRootPath, "Cascades");

//            _faceCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_frontalface_default.xml"));
//            _eyeCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_eye.xml"));
//            _noseCascade = new CascadeClassifier(Path.Combine(cascadePath, "nose.xml"));
//        }

//        [HttpPost("upload")]
//        public async Task<IActionResult> UploadFile([FromForm] FileUploadModel file)
//        {
//            if (file == null || file.File == null || file.File.Length == 0)
//                return BadRequest("Dosya yüklenemedi.");

//            var jobId = Guid.NewGuid().ToString();

//            string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");
//            string tempFolder = Path.Combine(_env.WebRootPath, "TempResults", jobId);
//            string resultsFolder = Path.Combine(_env.WebRootPath, "Results");
//            string zipPath = Path.Combine(resultsFolder, $"{jobId}.zip");
//            string tempZipPath = zipPath + ".tmp";

//            Directory.CreateDirectory(uploadsFolder);
//            Directory.CreateDirectory(tempFolder);
//            Directory.CreateDirectory(resultsFolder);

//            var ext = Path.GetExtension(file.File.FileName).ToLower();
//            var supportedImageExts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
//            var supportedVideoExts = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv" };

//            string inputPath = Path.Combine(uploadsFolder, $"{jobId}{ext}");
//            using (var fs = new FileStream(inputPath, FileMode.Create))
//            {
//                await file.File.CopyToAsync(fs);
//            }

//            bool yazildi = _queue.Writer.TryWrite(async token =>
//            {
//                try
//                {
//                    if (Array.Exists(supportedImageExts, e => e == ext))
//                    {
//                        await ProcessImageAsync(inputPath, tempFolder, file.CropEyes, file.CropNose, file.CropFace);
//                    }
//                    else if (Array.Exists(supportedVideoExts, e => e == ext))
//                    {
//                        await ProcessVideoAsync(inputPath, tempFolder, file.CropEyes, file.CropNose, file.CropFace);
//                    }
//                    else
//                    {
//                        throw new Exception("Desteklenmeyen dosya formatı.");
//                    }

//                    if (System.IO.File.Exists(tempZipPath)) System.IO.File.Delete(tempZipPath);
//                    if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);

//                    ZipFile.CreateFromDirectory(tempFolder, tempZipPath);
//                    System.IO.File.Move(tempZipPath, zipPath);

//                    try
//                    {
//                        Directory.Delete(tempFolder, true);
//                        System.IO.File.Delete(inputPath);
//                    }
//                    catch { }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Job {jobId} hata: {ex}");
//                }
//            });

//            if (!yazildi)
//                return StatusCode(503, "İşlem kuyruğa alınamadı, lütfen tekrar deneyin.");

//            return Ok(new { jobId, message = "Dosya kuyruğa alındı, sonucu GET /api/SelectedFeatures/result/{jobId} ile sorgulayabilirsiniz." });
//        }

//        [HttpGet("result/{jobId}")]
//        public IActionResult GetResult(string jobId)
//        {
//            string zipPath = Path.Combine(_env.WebRootPath, "Results", $"{jobId}.zip");

//            if (!System.IO.File.Exists(zipPath))
//                return StatusCode(202, new { status = "pending", message = "İşlem devam ediyor." });

//            var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
//            return File(stream, "application/zip", $"{jobId}.zip");
//        }

//        private Task ProcessImageAsync(string inputPath, string outputDir, bool cropEyes, bool cropNose, bool cropFace)
//        {
//            using var image = new Image<Bgr, byte>(inputPath);
//            using var gray = image.Convert<Gray, byte>();

//            var faces = _faceCascade.DetectMultiScale(gray, 1.05, 4, new Size(30, 30));
//            int count = 0;

//            foreach (var face in faces)
//            {
//                using var faceROIColor = image.Copy(face);
//                using var faceROIGray = faceROIColor.Convert<Gray, byte>();

//                if (cropEyes)
//                {
//                    var eyes = _eyeCascade.DetectMultiScale(faceROIGray, 1.05, 3, new Size(15, 15));
//                    foreach (var eye in eyes)
//                    {
//                        using var eyeImg = faceROIColor.Copy(eye);
//                        eyeImg.Save(Path.Combine(outputDir, $"eye_{count}.jpg"));
//                        count++;
//                    }
//                }

//                if (cropNose)
//                {
//                    var noses = _noseCascade.DetectMultiScale(faceROIGray, 1.05, 3, new Size(15, 15));
//                    foreach (var nose in noses)
//                    {
//                        using var noseImg = faceROIColor.Copy(nose);
//                        noseImg.Save(Path.Combine(outputDir, $"nose_{count}.jpg"));
//                        count++;
//                    }
//                }

//                if (cropFace)
//                {
//                    faceROIColor.Save(Path.Combine(outputDir, $"face_{count}.jpg"));
//                    count++;
//                }
//            }

//            return Task.CompletedTask;
//        }

//        private Task ProcessVideoAsync(string inputPath, string outputDir, bool cropEyes, bool cropNose, bool cropFace)
//        {
//            using var capture = new VideoCapture(inputPath);
//            int frameIndex = 0;
//            Mat frame = new Mat();

//            while (capture.Read(frame) && !frame.IsEmpty)
//            {
//                using var image = frame.ToImage<Bgr, byte>();
//                using var gray = image.Convert<Gray, byte>();

//                var faces = _faceCascade.DetectMultiScale(gray, 1.05, 4, new Size(30, 30));

//                foreach (var faceRect in faces)
//                {
//                    using var faceROIColor = image.Copy(faceRect);
//                    using var faceROIGray = faceROIColor.Convert<Gray, byte>();

//                    if (cropEyes)
//                    {
//                        var eyes = _eyeCascade.DetectMultiScale(faceROIGray, 1.05, 3, new Size(15, 15));
//                        foreach (var eyeRect in eyes)
//                        {
//                            using var eyeImg = faceROIColor.Copy(eyeRect);
//                            string fileName = $"frame_{frameIndex}_eye_{Guid.NewGuid()}.jpg";
//                            eyeImg.Save(Path.Combine(outputDir, fileName));
//                        }
//                    }

//                    if (cropNose)
//                    {
//                        var noses = _noseCascade.DetectMultiScale(faceROIGray, 1.05, 3, new Size(15, 15));
//                        foreach (var noseRect in noses)
//                        {
//                            using var noseImg = faceROIColor.Copy(noseRect);
//                            string fileName = $"frame_{frameIndex}_nose_{Guid.NewGuid()}.jpg";
//                            noseImg.Save(Path.Combine(outputDir, fileName));
//                        }
//                    }

//                    if (cropFace)
//                    {
//                        string faceFileName = $"frame_{frameIndex}_face_{Guid.NewGuid()}.jpg";
//                        faceROIColor.Save(Path.Combine(outputDir, faceFileName));
//                    }
//                }

//                frameIndex++;
//                if (frameIndex > 900) break;
//            }

//            return Task.CompletedTask;
//        }

//        public class FileUploadModel
//        {
//            [FromForm]
//            public IFormFile File { get; set; } = null!;

//            [FromForm]
//            public bool CropEyes { get; set; } = true;

//            [FromForm]
//            public bool CropNose { get; set; } = true;

//            [FromForm]
//            public bool CropFace { get; set; } = true;
//        }
//    }
//}


//using Emgu.CV;
//using Emgu.CV.Structure;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Drawing;
//using System.IO;
//using System.IO.Compression;
//using System.Threading;
//using System.Threading.Channels;
//using System.Threading.Tasks;

//namespace FotoFromFaceControl.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class SelectedFeaturesController : ControllerBase
//    {
//        private readonly CascadeClassifier _faceCascade;
//        private readonly CascadeClassifier _eyeCascade;
//        private readonly CascadeClassifier _noseCascade;
//        private readonly IWebHostEnvironment _env;
//        private readonly Channel<Func<CancellationToken, Task>> _queue;

//        public SelectedFeaturesController(Channel<Func<CancellationToken, Task>> queue, IWebHostEnvironment env)
//        {
//            _queue = queue;
//            _env = env;

//            string cascadePath = Path.Combine(env.WebRootPath, "Cascades");

//            _faceCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_frontalface_default.xml"));
//            _eyeCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_eye.xml"));
//            _noseCascade = new CascadeClassifier(Path.Combine(cascadePath, "nose.xml"));
//        }

//        [HttpPost("upload")]
//        public async Task<IActionResult> UploadFile([FromForm] FileUploadModel file)
//        {
//            if (file == null || file.File == null || file.File.Length == 0)
//                return BadRequest("Dosya yüklenemedi.");

//            var jobId = Guid.NewGuid().ToString();

//            string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");
//            string tempFolder = Path.Combine(_env.WebRootPath, "TempResults", jobId);
//            string resultsFolder = Path.Combine(_env.WebRootPath, "Results");
//            string zipPath = Path.Combine(resultsFolder, $"{jobId}.zip");
//            string tempZipPath = zipPath + ".tmp";

//            Directory.CreateDirectory(uploadsFolder);
//            Directory.CreateDirectory(tempFolder);
//            Directory.CreateDirectory(resultsFolder);

//            var ext = Path.GetExtension(file.File.FileName).ToLower();
//            var supportedImageExts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
//            var supportedVideoExts = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv" };

//            string inputPath = Path.Combine(uploadsFolder, $"{jobId}{ext}");
//            using (var fs = new FileStream(inputPath, FileMode.Create))
//            {
//                await file.File.CopyToAsync(fs);
//            }

//            bool yazildi = _queue.Writer.TryWrite(async token =>
//            {
//                try
//                {
//                    if (Array.Exists(supportedImageExts, e => e == ext))
//                    {
//                        await ProcessImageAsync(inputPath, tempFolder, file.CropEyes, file.CropNose, file.CropFace,
//                                                file.EyeScale, file.NoseScale, file.FaceScale);
//                    }
//                    else if (Array.Exists(supportedVideoExts, e => e == ext))
//                    {
//                        await ProcessVideoAsync(inputPath, tempFolder, file.CropEyes, file.CropNose, file.CropFace,
//                                                file.EyeScale, file.NoseScale, file.FaceScale);
//                    }
//                    else
//                    {
//                        throw new Exception("Desteklenmeyen dosya formatı.");
//                    }

//                    if (System.IO.File.Exists(tempZipPath)) System.IO.File.Delete(tempZipPath);
//                    if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);

//                    ZipFile.CreateFromDirectory(tempFolder, tempZipPath);
//                    System.IO.File.Move(tempZipPath, zipPath);

//                    try
//                    {
//                        Directory.Delete(tempFolder, true);
//                        System.IO.File.Delete(inputPath);
//                    }
//                    catch { }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Job {jobId} hata: {ex}");
//                }
//            });

//            if (!yazildi)
//                return StatusCode(503, "İşlem kuyruğa alınamadı, lütfen tekrar deneyin.");

//            return Ok(new { jobId, message = "Dosya kuyruğa alındı, sonucu GET /api/SelectedFeatures/result/{jobId} ile sorgulayabilirsiniz." });
//        }

//        [HttpGet("result/{jobId}")]
//        public IActionResult GetResult(string jobId)
//        {
//            string zipPath = Path.Combine(_env.WebRootPath, "Results", $"{jobId}.zip");

//            if (!System.IO.File.Exists(zipPath))
//                return StatusCode(202, new { status = "pending", message = "İşlem devam ediyor." });

//            var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
//            return File(stream, "application/zip", $"{jobId}.zip");
//        }

//        private Task ProcessImageAsync(string inputPath, string outputDir, bool cropEyes, bool cropNose, bool cropFace,
//                                       double eyeScale, double noseScale, double faceScale)
//        {
//            using var image = new Image<Bgr, byte>(inputPath);
//            using var gray = image.Convert<Gray, byte>();

//            var faces = _faceCascade.DetectMultiScale(gray, faceScale, 4, new Size(30, 30));
//            int count = 0;

//            foreach (var face in faces)
//            {
//                using var faceROIColor = image.Copy(face);
//                using var faceROIGray = faceROIColor.Convert<Gray, byte>();

//                if (cropEyes)
//                {
//                    var eyes = _eyeCascade.DetectMultiScale(faceROIGray, eyeScale, 3, new Size(15, 15));
//                    foreach (var eye in eyes)
//                    {
//                        using var eyeImg = faceROIColor.Copy(eye);
//                        eyeImg.Save(Path.Combine(outputDir, $"eye_{count}.jpg"));
//                        count++;
//                    }
//                }

//                if (cropNose)
//                {
//                    var noses = _noseCascade.DetectMultiScale(faceROIGray, noseScale, 3, new Size(15, 15));
//                    foreach (var nose in noses)
//                    {
//                        using var noseImg = faceROIColor.Copy(nose);
//                        noseImg.Save(Path.Combine(outputDir, $"nose_{count}.jpg"));
//                        count++;
//                    }
//                }

//                if (cropFace)
//                {
//                    faceROIColor.Save(Path.Combine(outputDir, $"face_{count}.jpg"));
//                    count++;
//                }
//            }

//            return Task.CompletedTask;
//        }

//        private Task ProcessVideoAsync(string inputPath, string outputDir, bool cropEyes, bool cropNose, bool cropFace,
//                                       double eyeScale, double noseScale, double faceScale)
//        {
//            using var capture = new VideoCapture(inputPath);
//            int frameIndex = 0;
//            Mat frame = new Mat();

//            while (capture.Read(frame) && !frame.IsEmpty)
//            {
//                using var image = frame.ToImage<Bgr, byte>();
//                using var gray = image.Convert<Gray, byte>();

//                var faces = _faceCascade.DetectMultiScale(gray, faceScale, 4, new Size(30, 30));

//                foreach (var faceRect in faces)
//                {
//                    using var faceROIColor = image.Copy(faceRect);
//                    using var faceROIGray = faceROIColor.Convert<Gray, byte>();

//                    if (cropEyes)
//                    {
//                        var eyes = _eyeCascade.DetectMultiScale(faceROIGray, eyeScale, 3, new Size(15, 15));
//                        foreach (var eyeRect in eyes)
//                        {
//                            using var eyeImg = faceROIColor.Copy(eyeRect);
//                            string fileName = $"frame_{frameIndex}_eye_{Guid.NewGuid()}.jpg";
//                            eyeImg.Save(Path.Combine(outputDir, fileName));
//                        }
//                    }

//                    if (cropNose)
//                    {
//                        var noses = _noseCascade.DetectMultiScale(faceROIGray, noseScale, 3, new Size(15, 15));
//                        foreach (var noseRect in noses)
//                        {
//                            using var noseImg = faceROIColor.Copy(noseRect);
//                            string fileName = $"frame_{frameIndex}_nose_{Guid.NewGuid()}.jpg";
//                            noseImg.Save(Path.Combine(outputDir, fileName));
//                        }
//                    }

//                    if (cropFace)
//                    {
//                        string faceFileName = $"frame_{frameIndex}_face_{Guid.NewGuid()}.jpg";
//                        faceROIColor.Save(Path.Combine(outputDir, faceFileName));
//                    }
//                }

//                frameIndex++;
//                if (frameIndex > 900) break;
//            }

//            return Task.CompletedTask;
//        }

//        public class FileUploadModel
//        {
//            [FromForm]
//            public IFormFile File { get; set; } = null!;

//            [FromForm]
//            public bool CropEyes { get; set; } = true;

//            [FromForm]
//            public bool CropNose { get; set; } = true;

//            [FromForm]
//            public bool CropFace { get; set; } = true;

//            [FromForm]
//            public double EyeScale { get; set; } = 1.05;

//            [FromForm]
//            public double NoseScale { get; set; } = 1.05;

//            [FromForm]
//            public double FaceScale { get; set; } = 1.05;
//        }
//    }
//}




































using Emgu.CV;
using Emgu.CV.Structure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
    public class SelectedFeaturesController : ControllerBase
    {
        private readonly CascadeClassifier _faceCascade;
        private readonly CascadeClassifier _eyeCascade;
        private readonly CascadeClassifier _noseCascade;
        private readonly IWebHostEnvironment _env;
        private readonly Channel<Func<CancellationToken, Task>> _queue;

        public SelectedFeaturesController(Channel<Func<CancellationToken, Task>> queue, IWebHostEnvironment env)
        {
            _queue = queue;
            _env = env;

            string cascadePath = Path.Combine(env.WebRootPath, "Cascades");

            _faceCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_frontalface_default.xml"));
            _eyeCascade = new CascadeClassifier(Path.Combine(cascadePath, "haarcascade_eye.xml"));
            _noseCascade = new CascadeClassifier(Path.Combine(cascadePath, "nose.xml"));
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] FileUploadModel file)
        {
            if (file == null || file.File == null || file.File.Length == 0)
                return BadRequest("Dosya yüklenemedi.");

            var jobId = Guid.NewGuid().ToString();

            string uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads");
            string tempFolder = Path.Combine(_env.WebRootPath, "TempResults", jobId);
            string resultsFolder = Path.Combine(_env.WebRootPath, "Results");
            string zipPath = Path.Combine(resultsFolder, $"{jobId}.zip");
            string tempZipPath = zipPath + ".tmp";

            Directory.CreateDirectory(uploadsFolder);
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(resultsFolder);

            var ext = Path.GetExtension(file.File.FileName).ToLower();
            var supportedImageExts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var supportedVideoExts = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv" };

            string inputPath = Path.Combine(uploadsFolder, $"{jobId}{ext}");
            using (var fs = new FileStream(inputPath, FileMode.Create))
            {
                await file.File.CopyToAsync(fs);
            }

            bool yazildi = _queue.Writer.TryWrite(async token =>
            {
                try
                {
                    if (Array.Exists(supportedImageExts, e => e == ext))
                    {
                        await ProcessImageAsync(inputPath, tempFolder, file.CropEyes, file.CropNose, file.CropFace, file.EyeScale, file.NoseScale, file.FaceScale);
                    }
                    else if (Array.Exists(supportedVideoExts, e => e == ext))
                    {
                        await ProcessVideoAsync(inputPath, tempFolder, file.CropEyes, file.CropNose, file.CropFace, file.EyeScale, file.NoseScale, file.FaceScale);
                    }
                    else
                    {
                        throw new Exception("Desteklenmeyen dosya formatı.");
                    }

                    if (System.IO.File.Exists(tempZipPath)) System.IO.File.Delete(tempZipPath);
                    if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);

                    ZipFile.CreateFromDirectory(tempFolder, tempZipPath);
                    System.IO.File.Move(tempZipPath, zipPath);

                    try
                    {
                        Directory.Delete(tempFolder, true);
                        System.IO.File.Delete(inputPath);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Job {jobId} hata: {ex}");
                }
            });

            if (!yazildi)
                return StatusCode(503, "İşlem kuyruğa alınamadı, lütfen tekrar deneyin.");

            return Ok(new { jobId, message = "Dosya kuyruğa alındı, sonucu GET /api/SelectedFeatures/result/{jobId} ile sorgulayabilirsiniz." });
        }

        [HttpGet("result/{jobId}")]
        public IActionResult GetResult(string jobId)
        {
            string zipPath = Path.Combine(_env.WebRootPath, "Results", $"{jobId}.zip");

            if (!System.IO.File.Exists(zipPath))
                return StatusCode(202, new { status = "pending", message = "İşlem devam ediyor." });

            var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(stream, "application/zip", $"{jobId}.zip");
        }

        private Task ProcessImageAsync(string inputPath, string outputDir,
            bool cropEyes, bool cropNose, bool cropFace,
            double eyeScale, double noseScale, double faceScale)
        {
            using var image = new Image<Bgr, byte>(inputPath);
            using var gray = image.Convert<Gray, byte>();

            var faces = _faceCascade.DetectMultiScale(gray, faceScale, 4, new Size(30, 30));
            int count = 0;

            foreach (var face in faces)
            {
                using var faceROIColor = image.Copy(face);
                using var faceROIGray = faceROIColor.Convert<Gray, byte>();

                if (cropEyes)
                {
                    var eyes = _eyeCascade.DetectMultiScale(faceROIGray, eyeScale, 3, new Size(15, 15));
                    foreach (var eye in eyes)
                    {
                        using var eyeImg = faceROIColor.Copy(eye);
                        eyeImg.Save(Path.Combine(outputDir, $"eye_{count}.jpg"));
                        count++;
                    }
                }

                if (cropNose)
                {
                    var noses = _noseCascade.DetectMultiScale(faceROIGray, noseScale, 3, new Size(15, 15));
                    foreach (var nose in noses)
                    {
                        using var noseImg = faceROIColor.Copy(nose);
                        noseImg.Save(Path.Combine(outputDir, $"nose_{count}.jpg"));
                        count++;
                    }
                }

                if (cropFace)
                {
                    faceROIColor.Save(Path.Combine(outputDir, $"face_{count}.jpg"));
                    count++;
                }
            }

            return Task.CompletedTask;
        }

        private Task ProcessVideoAsync(string inputPath, string outputDir,
            bool cropEyes, bool cropNose, bool cropFace,
            double eyeScale, double noseScale, double faceScale)
        {
            using var capture = new VideoCapture(inputPath);
            int frameIndex = 0;
            Mat frame = new Mat();

            while (capture.Read(frame) && !frame.IsEmpty)
            {
                using var image = frame.ToImage<Bgr, byte>();
                using var gray = image.Convert<Gray, byte>();

                var faces = _faceCascade.DetectMultiScale(gray, faceScale, 4, new Size(30, 30));

                foreach (var faceRect in faces)
                {
                    using var faceROIColor = image.Copy(faceRect);
                    using var faceROIGray = faceROIColor.Convert<Gray, byte>();

                    if (cropEyes)
                    {
                        var eyes = _eyeCascade.DetectMultiScale(faceROIGray, eyeScale, 3, new Size(15, 15));
                        foreach (var eyeRect in eyes)
                        {
                            using var eyeImg = faceROIColor.Copy(eyeRect);
                            string fileName = $"frame_{frameIndex}_eye_{Guid.NewGuid()}.jpg";
                            eyeImg.Save(Path.Combine(outputDir, fileName));
                        }
                    }

                    if (cropNose)
                    {
                        var noses = _noseCascade.DetectMultiScale(faceROIGray, noseScale, 3, new Size(15, 15));
                        foreach (var noseRect in noses)
                        {
                            using var noseImg = faceROIColor.Copy(noseRect);
                            string fileName = $"frame_{frameIndex}_nose_{Guid.NewGuid()}.jpg";
                            noseImg.Save(Path.Combine(outputDir, fileName));
                        }
                    }

                    if (cropFace)
                    {
                        string faceFileName = $"frame_{frameIndex}_face_{Guid.NewGuid()}.jpg";
                        faceROIColor.Save(Path.Combine(outputDir, faceFileName));
                    }
                }

                frameIndex++;
                if (frameIndex > 900) break;
            }

            return Task.CompletedTask;
        }

        public class FileUploadModel
        {
            [FromForm]
            public IFormFile File { get; set; } = null!;

            [FromForm]
            public bool CropEyes { get; set; } = true;

            [FromForm]
            public bool CropNose { get; set; } = true;

            [FromForm]
            public bool CropFace { get; set; } = true;

            [FromForm]
            public double EyeScale { get; set; } = 1.05;

            [FromForm]
            public double NoseScale { get; set; } = 1.05;

            [FromForm]
            public double FaceScale { get; set; } = 1.05;
        }
    }
}


