//using Emgu.CV;
//using Emgu.CV.Structure;
//using FotoFromFaceControl.Models;
//using Microsoft.AspNetCore.Mvc;
//using System.Drawing.Imaging;
//using System.IO.Compression;

//namespace FotoFromFaceControl.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class DetectFaceFeaturesController : ControllerBase
//    {
//        [HttpPost("upload")]
//        [Consumes("multipart/form-data")]
//        public IActionResult UploadPhoto([FromForm] FileUploadModel photo)
//        {
//            if (photo == null || photo.File == null || photo.File.Length == 0)
//                return BadRequest("Fotoğraf yüklenmedi.");

//            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
//            if (!Directory.Exists(uploadsFolder))
//                Directory.CreateDirectory(uploadsFolder);

//            var uniqueFileName = $"{Guid.NewGuid()}_{photo.File.FileName}";
//            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

//            using (var stream = new FileStream(filePath, FileMode.Create))
//            {
//                photo.File.CopyTo(stream);
//            }

//            var url = $"{Request.Scheme}://{Request.Host}/uploads/{uniqueFileName}";
//            return Ok(new { url });
//        }

//        [HttpPost("process")]
//        public IActionResult ProcessPhoto([FromBody] ProcessRequestModel request, [FromQuery] bool iscropped = false)
//        {
//            if (string.IsNullOrEmpty(request.ImageUrl))
//                return BadRequest("ImageUrl boş olamaz.");

//            var fileName = Path.GetFileName(new Uri(request.ImageUrl).LocalPath);
//            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);

//            if (!System.IO.File.Exists(filePath))
//                return NotFound("Dosya bulunamadı.");

//            using var image = new Image<Bgr, byte>(filePath);
//            var gray = image.Convert<Gray, byte>();

//            var faceCascadePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cascades", "haarcascade_frontalface_default.xml");
//            var eyeCascadePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cascades", "haarcascade_eye.xml");
//            var noseCascadePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cascades", "nose.xml");

//            var faceCascade = new CascadeClassifier(faceCascadePath);
//            var eyeCascade = new CascadeClassifier(eyeCascadePath);
//            var noseCascade = new CascadeClassifier(noseCascadePath);

//            var faces = faceCascade.DetectMultiScale(gray, 1.1, 4);
//            if (faces.Length == 0)
//                return BadRequest("Yüz bulunamadı.");

//            if (!iscropped)
//            {
//                foreach (var face in faces)
//                {
//                    image.Draw(face, new Bgr(System.Drawing.Color.Red), 3);

//                    var faceROI = gray.GetSubRect(face);

//                    var eyes = eyeCascade.DetectMultiScale(faceROI, 1.1, 4);
//                    foreach (var eye in eyes)
//                    {
//                        var eyeRectGlobal = new System.Drawing.Rectangle(
//                            face.X + eye.X,
//                            face.Y + eye.Y,
//                            eye.Width,
//                            eye.Height);
//                        image.Draw(eyeRectGlobal, new Bgr(System.Drawing.Color.Blue), 2);
//                    }

//                    var noses = noseCascade.DetectMultiScale(faceROI, 1.1, 4);
//                    foreach (var nose in noses)
//                    {
//                        var noseRectGlobal = new System.Drawing.Rectangle(
//                            face.X + nose.X,
//                            face.Y + nose.Y,
//                            nose.Width,
//                            nose.Height);
//                        image.Draw(noseRectGlobal, new Bgr(System.Drawing.Color.Green), 2);
//                    }
//                }

//                using var ms = new MemoryStream();
//                image.ToBitmap().Save(ms, ImageFormat.Jpeg);
//                ms.Position = 0;

//                return File(ms.ToArray(), "image/jpeg");
//            }
//            else
//            {
//                using var zipStream = new MemoryStream();
//                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
//                {
//                    int faceIndex = 1;
//                    foreach (var face in faces)
//                    {
//                        var faceImage = image.Copy(face);
//                        var faceEntry = archive.CreateEntry($"face_{faceIndex}.jpg");
//                        using (var entryStream = faceEntry.Open())
//                        {
//                            using var faceMs = new MemoryStream();
//                            faceImage.ToBitmap().Save(faceMs, ImageFormat.Jpeg);
//                            faceMs.Position = 0;
//                            faceMs.CopyTo(entryStream);
//                        }

//                        var faceROIGray = gray.GetSubRect(face);

//                        var eyes = eyeCascade.DetectMultiScale(faceROIGray, 1.1, 4);
//                        int eyeIndex = 1;
//                        foreach (var eye in eyes)
//                        {
//                            var eyeRectGlobal = new System.Drawing.Rectangle(
//                                face.X + eye.X,
//                                face.Y + eye.Y,
//                                eye.Width,
//                                eye.Height);

//                            var eyeImage = image.Copy(eyeRectGlobal);
//                            var eyeEntry = archive.CreateEntry($"face_{faceIndex}_eye_{eyeIndex}.jpg");
//                            using (var entryStream = eyeEntry.Open())
//                            {
//                                using var eyeMs = new MemoryStream();
//                                eyeImage.ToBitmap().Save(eyeMs, ImageFormat.Jpeg);
//                                eyeMs.Position = 0;
//                                eyeMs.CopyTo(entryStream);
//                            }
//                            eyeIndex++;
//                        }

//                        var noses = noseCascade.DetectMultiScale(faceROIGray, 1.1, 4);
//                        int noseIndex = 1;
//                        foreach (var nose in noses)
//                        {
//                            var noseRectGlobal = new System.Drawing.Rectangle(
//                                face.X + nose.X,
//                                face.Y + nose.Y,
//                                nose.Width,
//                                nose.Height);

//                            var noseImage = image.Copy(noseRectGlobal);
//                            var noseEntry = archive.CreateEntry($"face_{faceIndex}_nose_{noseIndex}.jpg");
//                            using (var entryStream = noseEntry.Open())
//                            {
//                                using var noseMs = new MemoryStream();
//                                noseImage.ToBitmap().Save(noseMs, ImageFormat.Jpeg);
//                                noseMs.Position = 0;
//                                noseMs.CopyTo(entryStream);
//                            }
//                            noseIndex++;
//                        }

//                        faceIndex++;
//                    }
//                }

//                zipStream.Position = 0;
//                return File(zipStream.ToArray(), "application/zip", "cropped_faces.zip");
//            }
//        }
//    }
//}
