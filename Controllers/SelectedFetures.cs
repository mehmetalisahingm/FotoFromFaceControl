using Emgu.CV;
using Emgu.CV.Structure;
using FotoFromFaceControl.Models;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.IO;
using System.IO.Compression;

[ApiController]
[Route("api/[controller]")]
public class DetectFaceFeaturesController : ControllerBase
{
    private readonly CascadeClassifier _faceCascade;
    private readonly CascadeClassifier _eyeCascade;
    private readonly CascadeClassifier _noseCascade;

    public DetectFaceFeaturesController(IWebHostEnvironment env)
    {
        string cascadeBasePath = Path.Combine(env.WebRootPath, "CasCades");
        _faceCascade = new CascadeClassifier(Path.Combine(cascadeBasePath, "haarcascade_frontalface_default.xml"));
        _eyeCascade = new CascadeClassifier(Path.Combine(cascadeBasePath, "haarcascade_eye.xml"));
        _noseCascade = new CascadeClassifier(Path.Combine(cascadeBasePath, "nose.xml"));
    }

    [HttpPost("upload-and-process")]
    public IActionResult UploadAndProcess(
        [FromForm] FileUploadModel photo,
        [FromQuery] bool cropface = false,
        [FromQuery] bool cropeyes = false,
        [FromQuery] bool cropnose = false,
        [FromQuery] float scaleFactor = 1.05f,
        [FromQuery] int minNeighbors = 3,
        [FromQuery] int minWidth = 30,
        [FromQuery] int minHeight = 30)
    {
        if (photo == null || photo.File == null)
            return BadRequest("Dosya eksik.");

        var ext = Path.GetExtension(photo.File.FileName).ToLower();
        var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
        if (!allowedExt.Contains(ext))
            return BadRequest("Lütfen jpg, jpeg, png veya bmp formatında bir resim yükleyin.");

        using var ms = new MemoryStream();
        photo.File.CopyTo(ms);
        ms.Position = 0;

        using var bitmap = new Bitmap(ms);
        using var img = bitmap.ToImage<Bgr, byte>();
        using var gray = img.Convert<Gray, byte>();

        var faces = _faceCascade.DetectMultiScale(gray, scaleFactor, minNeighbors, new Size(minWidth, minHeight));
        if (faces.Length == 0)
            return BadRequest("Yüz bulunamadı.");

        if (cropface || cropeyes || cropnose)
        {
            using var archiveStream = new MemoryStream();
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true);
            int count = 1;

            foreach (var face in faces)
            {
                if (cropface)
                {
                    using var croppedFace = img.Copy(face);
                    using var croppedBitmap = croppedFace.ToBitmap();
                    var entry = archive.CreateEntry($"face_{count}.jpg");
                    using var entryStream = entry.Open();
                    croppedBitmap.Save(entryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                    count++;
                }

                var faceROI = new Rectangle(face.X, face.Y, face.Width, face.Height);
                using var faceGray = new Mat(gray.Mat, faceROI);

                if (cropeyes)
                {
                    var eyes = _eyeCascade.DetectMultiScale(faceGray, scaleFactor, minNeighbors, new Size(minWidth / 2, minHeight / 2));
                    int eyeCount = 1;
                    foreach (var eye in eyes)
                    {
                        var rect = new Rectangle(face.X + eye.X, face.Y + eye.Y, eye.Width, eye.Height);
                        using var eyeImg = img.Copy(rect);
                        using var eyeBmp = eyeImg.ToBitmap();
                        var entry = archive.CreateEntry($"eye_{count}_{eyeCount}.jpg");
                        using var stream = entry.Open();
                        eyeBmp.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                        eyeCount++;
                    }
                }

                if (cropnose)
                {
                    var noses = _noseCascade.DetectMultiScale(faceGray, scaleFactor, minNeighbors, new Size(minWidth / 2, minHeight / 2));
                    int noseCount = 1;
                    foreach (var nose in noses)
                    {
                        var rect = new Rectangle(face.X + nose.X, face.Y + nose.Y, nose.Width, nose.Height);
                        using var noseImg = img.Copy(rect);
                        using var noseBmp = noseImg.ToBitmap();
                        var entry = archive.CreateEntry($"nose_{count}_{noseCount}.jpg");
                        using var stream = entry.Open();
                        noseBmp.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                        noseCount++;
                    }
                }
            }

            archiveStream.Position = 0;
            return File(archiveStream.ToArray(), "application/zip", "cropped_features.zip");
        }
        else
        {
            // Kırpma seçilmemişse, yüzlerin etrafına kutu çiz ve tek jpg olarak gönder
            foreach (var face in faces)
            {
                CvInvoke.Rectangle(img, face, new MCvScalar(0, 0, 255), 2);
            }

            using var outStream = new MemoryStream();
            img.ToBitmap().Save(outStream, System.Drawing.Imaging.ImageFormat.Jpeg);
            outStream.Position = 0;
            return File(outStream.ToArray(), "image/jpeg", "faces_marked.jpg");
        }
    }
}
