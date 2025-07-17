//using Emgu.CV;
//using Emgu.CV.Structure;
//using FotoFromFaceControl.Models;
//using Microsoft.AspNetCore.Mvc;
//using System.Drawing.Imaging;
//using System.IO.Compression;

//[HttpPost("upload-and-crop")]
//[Consumes("multipart/form-data")]
//public IActionResult UploadAndCrop(
//    [FromForm] FileUploadModel photo,
//    [FromQuery] bool cropeyes = false,
//    [FromQuery] bool cropnose = false)
//{
//    if (photo == null || photo.File == null || photo.File.Length == 0)
//        return BadRequest("Fotoğraf yüklenmedi.");

//    // Fotoğrafı kaydet
//    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
//    if (!Directory.Exists(uploadsFolder))
//        Directory.CreateDirectory(uploadsFolder);

//    var uniqueFileName = $"{Guid.NewGuid()}_{photo.File.FileName}";
//    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

//    using (var stream = new FileStream(filePath, FileMode.Create))
//    {
//        photo.File.CopyTo(stream);
//    }

//    using var image = new Image<Bgr, byte>(filePath);
//    var gray = image.Convert<Gray, byte>();

//    // XML yolları
//    var faceCascadePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cascades", "haarcascade_frontalface_default.xml");
//    var eyeCascadePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cascades", "haarcascade_eye.xml");
//    var noseCascadePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cascades", "nose.xml");

//    var faceCascade = new CascadeClassifier(faceCascadePath);
//    var eyeCascade = new CascadeClassifier(eyeCascadePath);
//    var noseCascade = new CascadeClassifier(noseCascadePath);

//    var faces = faceCascade.DetectMultiScale(gray, 1.1, 4);
//    if (faces.Length == 0)
//        return BadRequest("Yüz bulunamadı.");

//    // ZIP paketle
//    using var zipStream = new MemoryStream();
//    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
//    {
//        int faceIndex = 1;
//        foreach (var face in faces)
//        {
//            var faceROIGray = gray.GetSubRect(face);

//            if (cropeyes)
//            {
//                var eyes = eyeCascade.DetectMultiScale(faceROIGray, 1.1, 4);
//                int eyeIndex = 1;
//                foreach (var eye in eyes)
//                {
//                    var eyeRectGlobal = new System.Drawing.Rectangle(
//                        face.X + eye.X, face.Y + eye.Y, eye.Width, eye.Height);
//                    var eyeImage = image.Copy(eyeRectGlobal);
//                    var eyeEntry = archive.CreateEntry($"face_{faceIndex}_eye_{eyeIndex}.jpg");
//                    using var entryStream = eyeEntry.Open();
//                    using var ms = new MemoryStream();
//                    eyeImage.ToBitmap().Save(ms, ImageFormat.Jpeg);
//                    ms.Position = 0;
//                    ms.CopyTo(entryStream);
//                    eyeIndex++;
//                }
//            }

//            if (cropnose)
//            {
//                var noses = noseCascade.DetectMultiScale(faceROIGray, 1.1, 4);
//                int noseIndex = 1;
//                foreach (var nose in noses)
//                {
//                    var noseRectGlobal = new System.Drawing.Rectangle(
//                        face.X + nose.X, face.Y + nose.Y, nose.Width, nose.Height);
//                    var noseImage = image.Copy(noseRectGlobal);
//                    var noseEntry = archive.CreateEntry($"face_{faceIndex}_nose_{noseIndex}.jpg");
//                    using var entryStream = noseEntry.Open();
//                    using var ms = new MemoryStream();
//                    noseImage.ToBitmap().Save(ms, ImageFormat.Jpeg);
//                    ms.Position = 0;
//                    ms.CopyTo(entryStream);
//                    noseIndex++;
//                }
//            }

//            faceIndex++;
//        }
//    }

//    zipStream.Position = 0;
//    return File(zipStream.ToArray(), "application/zip", "cropped_features.zip");
//}
