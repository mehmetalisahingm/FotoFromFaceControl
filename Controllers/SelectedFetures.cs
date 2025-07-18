using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Emgu.CV;
using Emgu.CV.Structure;
using System.IO.Compression;
using System.Drawing;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;

namespace FotoFromFaceControl.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SelectedFeaturesController : ControllerBase
    {
        private readonly string _cascadePath;
        private readonly CascadeClassifier faceCascade;
        private readonly CascadeClassifier eyesCascade;
        private readonly CascadeClassifier noseCascade;

        public SelectedFeaturesController(IWebHostEnvironment env)
        {
            _cascadePath = Path.Combine(env.WebRootPath, "Cascades");
            faceCascade = new CascadeClassifier(Path.Combine(_cascadePath, "haarcascade_frontalface_default.xml"));
            eyesCascade = new CascadeClassifier(Path.Combine(_cascadePath, "haarcascade_eye.xml"));
            noseCascade = new CascadeClassifier(Path.Combine(_cascadePath, "nose.xml"));
        }

        [HttpPost("upload-and-process")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndProcess(
            [FromQuery] bool cropFace = true,
            [FromQuery] bool cropEyes = true,
            [FromQuery] bool cropNose = true,
            [FromQuery] double scaleFactor = 1.05,
            [FromQuery] int minNeighbors = 3,
            [FromQuery] int minWidth = 30,
            [FromQuery] int minHeight = 30,
            [FromQuery] double distance = 0)
        {
            if (Request.Form.Files.Count == 0)
                return BadRequest("Dosya bulunamadı.");

            var file = Request.Form.Files[0];
            string ext = Path.GetExtension(file.FileName).ToLower();

            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
                return BadRequest("Desteklenmeyen dosya formatı.");

            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            string tempFilePath = Path.Combine(tempFolder, file.FileName);

            using (var fs = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            try
            {
                using var img = new Image<Bgr, byte>(tempFilePath);

                var results = new List<(string fileName, Image<Bgr, byte> image)>();

                var faces = faceCascade.DetectMultiScale(img, scaleFactor, minNeighbors, new Size(minWidth, minHeight));

                foreach (var faceRect in faces)
                {
                    if (cropFace)
                    {
                        var faceImg = img.Copy(faceRect);
                        results.Add(("face_" + Guid.NewGuid() + ".png", faceImg));
                    }

                    if (cropEyes)
                    {
                        var faceROI = img.Copy(faceRect);
                        var eyes = eyesCascade.DetectMultiScale(faceROI, scaleFactor, minNeighbors, new Size(minWidth, minHeight));
                        foreach (var eyeRect in eyes)
                        {
                            var eyeImg = faceROI.Copy(eyeRect);
                            results.Add(("eye_" + Guid.NewGuid() + ".png", eyeImg));
                        }
                    }

                    if (cropNose)
                    {
                        var faceROI = img.Copy(faceRect);
                        var noses = noseCascade.DetectMultiScale(faceROI, scaleFactor, minNeighbors, new Size(minWidth, minHeight));
                        foreach (var noseRect in noses)
                        {
                            var noseImg = faceROI.Copy(noseRect);
                            results.Add(("nose_" + Guid.NewGuid() + ".png", noseImg));
                        }
                    }
                }

                if (results.Count == 0)
                    return NotFound("Hiçbir özellik tespit edilmedi.");

                string zipFileName = "selected_features_result.zip";
                string zipFilePath = Path.Combine(tempFolder, zipFileName);

                using (var zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    foreach (var (fileName, image) in results)
                    {
                        using var ms = new MemoryStream();
                        image.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        var entry = zip.CreateEntry(fileName);
                        using var entryStream = entry.Open();
                        ms.CopyTo(entryStream);
                    }
                }

                var zipBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);

                Directory.Delete(tempFolder, true);

                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
                return StatusCode(500, "İşlem sırasında hata: " + ex.Message);
            }
        }

        [HttpPost("upload-video")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadVideo(
            [FromQuery] bool cropFace = true,
            [FromQuery] bool cropEyes = true,
            [FromQuery] bool cropNose = true,
            [FromQuery] double scaleFactor = 1.05,
            [FromQuery] int minNeighbors = 3,
            [FromQuery] int minWidth = 30,
            [FromQuery] int minHeight = 30,
            [FromQuery] double distance = 0)
        {
            if (Request.Form.Files.Count == 0)
                return BadRequest("Dosya bulunamadı.");

            var file = Request.Form.Files[0];
            string ext = Path.GetExtension(file.FileName).ToLower();

            if (ext != ".mp4" && ext != ".avi" && ext != ".mov" && ext != ".wmv" && ext != ".mkv")
                return BadRequest("Desteklenmeyen video formatı.");

            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);
            string tempFilePath = Path.Combine(tempFolder, file.FileName);

            using (var fs = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            try
            {
                using var capture = new VideoCapture(tempFilePath);

                var results = new List<(string fileName, Image<Bgr, byte> image)>();

                int frameIndex = 0;
                Mat frame = new Mat();

                while (capture.Read(frame) && !frame.IsEmpty)
                {
                    using var image = frame.ToImage<Bgr, byte>();

                    var faces = faceCascade.DetectMultiScale(image, scaleFactor, minNeighbors, new Size(minWidth, minHeight));

                    foreach (var faceRect in faces)
                    {
                        if (cropFace)
                        {
                            var faceImg = image.Copy(faceRect);
                            results.Add(($"video_{frameIndex}_face_{Guid.NewGuid()}.png", faceImg));
                        }

                        if (cropEyes)
                        {
                            var faceROI = image.Copy(faceRect);
                            var eyes = eyesCascade.DetectMultiScale(faceROI, scaleFactor, minNeighbors, new Size(minWidth, minHeight));
                            foreach (var eyeRect in eyes)
                            {
                                var eyeImg = faceROI.Copy(eyeRect);
                                results.Add(($"video_{frameIndex}_eye_{Guid.NewGuid()}.png", eyeImg));
                            }
                        }

                        if (cropNose)
                        {
                            var faceROI = image.Copy(faceRect);
                            var noses = noseCascade.DetectMultiScale(faceROI, scaleFactor, minNeighbors, new Size(minWidth, minHeight));
                            foreach (var noseRect in noses)
                            {
                                var noseImg = faceROI.Copy(noseRect);
                                results.Add(($"video_{frameIndex}_nose_{Guid.NewGuid()}.png", noseImg));
                            }
                        }
                    }

                    frameIndex++;

                    // Çok fazla kare işlemek istemezsen sınır koyabilirsin:
                    if (frameIndex > 500) break;
                }

                if (results.Count == 0)
                    return NotFound("Hiçbir özellik tespit edilmedi.");

                string zipFileName = "selected_features_video_result.zip";
                string zipFilePath = Path.Combine(tempFolder, zipFileName);

                using (var zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    foreach (var (fileName, image) in results)
                    {
                        using var ms = new MemoryStream();
                        image.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        var entry = zip.CreateEntry(fileName);
                        using var entryStream = entry.Open();
                        ms.CopyTo(entryStream);
                    }
                }

                var zipBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);

                Directory.Delete(tempFolder, true);

                return File(zipBytes, "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
                return StatusCode(500, "Video işlem sırasında hata: " + ex.Message);
            }
        }
    }
}
