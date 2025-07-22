using DlibDotNet;
using DlibDotNet.Extensions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using FotoFromFaceControl.Models;

namespace FaceSimilarityApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FaceSimilarityController : ControllerBase
    {
        private readonly string _predictorPath;

        public FaceSimilarityController(IWebHostEnvironment env)
        {
            _predictorPath = Path.Combine(env.WebRootPath, "CasCades", "shape_predictor_68_face_landmarks.dat");
            if (!System.IO.File.Exists(_predictorPath))
                throw new FileNotFoundException("shape_predictor_68_face_landmarks (2).dat dosyası bulunamadı!", _predictorPath);
        }


        [HttpPost("compare")]
        public async Task<IActionResult> CompareFaces([FromForm] CompareFacesModel model)
        {
            if (model.File1 == null || model.File2 == null)
                return BadRequest("İki görsel de yüklenmelidir.");

            using var detector = Dlib.GetFrontalFaceDetector();
            using var predictor = ShapePredictor.Deserialize(_predictorPath);

            var landmarks1 = await GetLandmarksFromImageAsync(model.File1, detector, predictor);
            var landmarks2 = await GetLandmarksFromImageAsync(model.File2, detector, predictor);

            if (landmarks1 == null || landmarks2 == null)
                return BadRequest("Yüz tespiti yapılamadı.");

            if (landmarks1.Length != landmarks2.Length)
                return BadRequest("Landmark sayıları eşleşmiyor.");

            double similarity = CalculateSimilarity(landmarks1, landmarks2);

            return Ok(new { similarity = Math.Round(similarity * 100, 2) }); 
        }

        private async Task<PointF[]?> GetLandmarksFromImageAsync(IFormFile file, FrontalFaceDetector detector, ShapePredictor predictor)
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            using var bitmap = new Bitmap(ms);
            var array2D = bitmap.ToArray2D<RgbPixel>();

            var faces = detector.Operator(array2D);
            if (faces.Length == 0)
                return null;

            var shape = predictor.Detect(array2D, faces[0]);
            var points = new PointF[shape.Parts];
            for (uint i = 0; i < shape.Parts; i++)
            {
                var p = shape.GetPart(i);
                points[i] = new PointF(p.X, p.Y);
            }
            return points;
        }

        private double CalculateSimilarity(PointF[] pts1, PointF[] pts2)
        {
            double totalDistance = 0;
            for (int i = 0; i < pts1.Length; i++)
            {
                double dx = pts1[i].X - pts2[i].X;
                double dy = pts1[i].Y - pts2[i].Y;
                totalDistance += Math.Sqrt(dx * dx + dy * dy);
            }
            double avgDistance = totalDistance / pts1.Length;

        
            double similarity = Math.Max(0, 100 - avgDistance);

           
            return similarity / 100.0;
        }
    }
}
