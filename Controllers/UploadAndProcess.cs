using Emgu.CV;
using Emgu.CV.Structure;
using FotoFromFaceControl.Models;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.IO;
using System.IO.Compression;

[ApiController]
[Route("api/[controller]")]
public class DetectFaceFeaturesController1: ControllerBase
{
    private readonly CascadeClassifier _faceCascade;

    public DetectFaceFeaturesController1 (IWebHostEnvironment env)
    {
        var cascadePath = Path.Combine(env.WebRootPath, "CasCades", "haarcascade_frontalface_default.xml");
        _faceCascade = new CascadeClassifier(cascadePath);
    }

    [HttpPost("upload-and-process")]
    public IActionResult UploadAndProcess(
        [FromForm] FileUploadModel photo,
        [FromQuery] bool iscropped,
        [FromQuery] string faceName = "",
        [FromQuery] float scaleFactor = 1.05f,
        [FromQuery] int minNeighbors = 3,
        [FromQuery] int minWidth = 30,
        [FromQuery] int minHeight = 30)
    {
        if (photo == null || photo.File == null)
            return BadRequest("Dosya eksik.");

        var ext = Path.GetExtension(photo.File.FileName).ToLowerInvariant();

        if (ext == ".mp4")
        {
            using var videoStream = new MemoryStream();
            photo.File.CopyTo(videoStream);
            var tempPath = Path.GetTempFileName() + ".mp4";
            System.IO.File.WriteAllBytes(tempPath, videoStream.ToArray());

            using var capture = new VideoCapture(tempPath);
            using var archiveStream = new MemoryStream();
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true);

            int frameIndex = 0;
            Mat frame = new Mat();

            while (capture.Read(frame))
            {
                if (frame.IsEmpty) break;

                var img = frame.ToImage<Bgr, byte>();
                var gray = img.Convert<Gray, byte>();

                var faces = _faceCascade.DetectMultiScale(gray, scaleFactor, minNeighbors, new Size(minWidth, minHeight));
                foreach (var face in faces)
                {
                    if (iscropped)
                    {
                        using var cropped = img.Copy(face);
                        using var croppedBmp = cropped.ToBitmap();
                        var entry = archive.CreateEntry($"frame_{frameIndex}_face.jpg");
                        using var entryStream = entry.Open();
                        croppedBmp.Save(entryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                    else
                    {
                        CvInvoke.Rectangle(img, face, new MCvScalar(0, 0, 255), 2);
                        using var msOut = new MemoryStream();
                        img.ToBitmap().Save(msOut, System.Drawing.Imaging.ImageFormat.Jpeg);
                        var entry = archive.CreateEntry($"frame_{frameIndex}_drawn.jpg");
                        using var entryStream = entry.Open();
                        msOut.Position = 0;
                        msOut.CopyTo(entryStream);
                    }
                }

                frameIndex++;
            }

            archiveStream.Position = 0;
            return File(archiveStream.ToArray(), "application/zip", "video_result.zip");
        }
        else
        {
            using var ms = new MemoryStream();
            photo.File.CopyTo(ms);
            ms.Position = 0;

            using var bitmap = new Bitmap(ms);
            using var img = bitmap.ToImage<Bgr, byte>();
            var gray = img.Convert<Gray, byte>();

            var faces = _faceCascade.DetectMultiScale(gray, scaleFactor, minNeighbors, new Size(minWidth, minHeight));
            if (faces.Length == 0)
                return BadRequest("Yüz bulunamadı.");

            if (iscropped)
            {
                using var archiveStream = new MemoryStream();
                using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true);

                int count = 1;
                foreach (var face in faces)
                {
                    using var croppedFace = img.Copy(face);
                    using var croppedBitmap = croppedFace.ToBitmap();
                    var entry = archive.CreateEntry($"face_{count}.jpg");
                    using var entryStream = entry.Open();
                    croppedBitmap.Save(entryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                    count++;
                }

                archiveStream.Position = 0;
                return File(archiveStream.ToArray(), "application/zip", "cropped_faces.zip");
            }
            else
            {
                foreach (var face in faces)
                {
                    CvInvoke.Rectangle(img, face, new MCvScalar(0, 0, 255), 2);
                }

                using var msOut = new MemoryStream();
                img.ToBitmap().Save(msOut, System.Drawing.Imaging.ImageFormat.Jpeg);
                msOut.Position = 0;
                return File(msOut.ToArray(), "image/jpeg", "result_with_faces.jpg");
            }
        }
    }
}
