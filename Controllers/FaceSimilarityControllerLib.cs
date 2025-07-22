using Microsoft.AspNetCore.Mvc;
using FaceRecognitionDotNet;
using System.Drawing;
using System.IO;
using System.Linq;

namespace FotoFromFaceControl.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FaceSimilarityController : ControllerBase
    {
        private readonly string _modelsPath;
        private readonly string _libraryPath;
        private readonly string _uploadPath;
        private readonly FaceRecognition _faceRecognition;

        public FaceSimilarityController(IWebHostEnvironment env)
        {
            _modelsPath = Path.Combine(env.ContentRootPath, "wwwroot", "models");
            _libraryPath = Path.Combine(env.ContentRootPath, "wwwroot", "FaceLibrary");
            _uploadPath = Path.Combine(env.ContentRootPath, "wwwroot", "Uploads");

            Directory.CreateDirectory(_uploadPath);
            _faceRecognition = FaceRecognition.Create(_modelsPath);
        }

        [HttpPost("find-most-similar")]
        public IActionResult FindMostSimilar([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Dosya yüklenemedi.");

            var uploadedFilePath = Path.Combine(_uploadPath, Guid.NewGuid() + Path.GetExtension(file.FileName));
            using (var stream = new FileStream(uploadedFilePath, FileMode.Create))
                file.CopyTo(stream);

            try
            {
                using var image = FaceRecognition.LoadImageFile(uploadedFilePath);
                var uploadedEncodings = _faceRecognition.FaceEncodings(image).ToList();
                if (!uploadedEncodings.Any())
                    return BadRequest("Yüz algılanamadı.");

                var uploadedEncoding = uploadedEncodings.First();

                string bestMatchFile = null;
                double bestScore = -1;

                foreach (var filePath in Directory.GetFiles(_libraryPath))
                {
                    try
                    {
                        using var libImg = FaceRecognition.LoadImageFile(filePath);
                        var libEncodings = _faceRecognition.FaceEncodings(libImg).ToList();
                        if (!libEncodings.Any())
                            continue;

                        double distance = FaceRecognition.FaceDistance(libEncodings.First(), uploadedEncoding);
                        double similarity = 1.0 - distance;

                        if (similarity > bestScore)
                        {
                            bestScore = similarity;
                            bestMatchFile = Path.GetFileName(filePath);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (bestMatchFile == null)
                    return NotFound("Benzer yüz bulunamadı.");

                return Ok(new
                {
                    EnBenzerResim = bestMatchFile,
                    BenzerlikOrani = Math.Round(bestScore * 100, 2)
                });
            }
            finally
            {
                System.IO.File.Delete(uploadedFilePath);
            }
        }
        public class FileUploadModel
        {
            public IFormFile File { get; set; } = null!;
        }
    }
}
