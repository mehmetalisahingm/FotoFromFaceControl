using FaceRecognitionDotNet;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace FotoFromFaceControl.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DynamicFaceLibraryController : ControllerBase
    {
        private readonly string _modelsPath;
        private readonly FaceRecognition _faceRecognition;

        public DynamicFaceLibraryController(IWebHostEnvironment env)
        {
            _modelsPath = Path.Combine(env.ContentRootPath, "wwwroot", "models");
            _faceRecognition = FaceRecognition.Create(_modelsPath);
        }

        [HttpPost("find-most-similar-from-zip")]
        public async Task<IActionResult> FindMostSimilarFromZip([FromForm] FileUploadModel model)
        {
            if (model == null || model.Photo == null || model.Photo.Length == 0)
                return BadRequest("Fotoğraf yüklenmedi.");

            if (model.LibraryZip == null || model.LibraryZip.Length == 0)
                return BadRequest("Kütüphane ZIP dosyası yüklenmedi.");

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            string uploadedPhotoPath = Path.Combine(tempDir, "uploaded_photo" + Path.GetExtension(model.Photo.FileName));
            string extractedDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractedDir);

            try
            {
                // 1) Kaydet gönderilen fotoğrafı
                using (var fs = new FileStream(uploadedPhotoPath, FileMode.Create))
                {
                    await model.Photo.CopyToAsync(fs);
                }

                // 2) Zip dosyasını aç ve fotoğrafları çıkar
                using (var zipStream = model.LibraryZip.OpenReadStream())
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                        {
                            string filePath = Path.Combine(extractedDir, Path.GetFileName(entry.FullName));
                            using var entryStream = entry.Open();
                            using var fs = new FileStream(filePath, FileMode.Create);
                            await entryStream.CopyToAsync(fs);
                        }
                    }
                }

                // 3) Yüklenen fotoğrafın yüz encoding'ini al
                using var uploadedImage = FaceRecognition.LoadImageFile(uploadedPhotoPath);
                var uploadedEncodings = _faceRecognition.FaceEncodings(uploadedImage).ToList();
                if (!uploadedEncodings.Any())
                    return BadRequest("Gönderilen fotoğrafta yüz bulunamadı.");

                var uploadedEncoding = uploadedEncodings.First();

                // 4) Kütüphane fotoğrafları içinde en benzerini bul
                string bestMatchFile = null;
                double bestSimilarity = -1;

                foreach (var file in Directory.GetFiles(extractedDir))
                {
                    try
                    {
                        using var libImage = FaceRecognition.LoadImageFile(file);
                        var libEncodings = _faceRecognition.FaceEncodings(libImage).ToList();
                        if (!libEncodings.Any())
                            continue;

                        double distance = FaceRecognition.FaceDistance(libEncodings.First(), uploadedEncoding);
                        double similarity = 1.0 - distance;

                        if (similarity > bestSimilarity)
                        {
                            bestSimilarity = similarity;
                            bestMatchFile = file;
                        }
                    }
                    catch
                    {
                        // Hata varsa dosyayı atla
                        continue;
                    }
                }

                if (bestMatchFile == null)
                    return NotFound("Benzer yüz bulunamadı.");

                // 5) Dosyayı ve benzerlik oranını dön
                var fileBytes = await System.IO.File.ReadAllBytesAsync(bestMatchFile);
                var mimeType = GetMimeType(bestMatchFile);
                Response.Headers.Add("X-Benzerlik-Orani", (bestSimilarity * 100).ToString("F2"));

                return File(fileBytes, mimeType, Path.GetFileName(bestMatchFile));
            }
            finally
            {
                // Geçici klasörü temizle
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private string GetMimeType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        public class FileUploadModel
        {
            public IFormFile Photo { get; set; } = null!;
            public IFormFile LibraryZip { get; set; } = null!;
        }
    }
}
