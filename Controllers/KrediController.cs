using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

namespace deneme.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KrediController : Controller
    {
        private readonly IConfiguration _configuration;

        public KrediController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("istatistikler")]
        public IActionResult GetIstatistikler()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "Select OnayDurumu, COUNT(*) as Howmany, Cast(ROUND(count(*) * 100.0 / (select count(*) from KrediBasvuru),2) AS DECIMAL(10, 2)) as percentage from KrediBasvuru GROUP BY OnayDurumu";
                    // Önce veritabanındaki tabloları listeleyelim
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            var istatistikler = new List<Dictionary<string, object>>();
                            while (reader.Read())
                            {
                                var istatistik = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    istatistik.Add(reader.GetName(i), reader.GetValue(i));
                                }
                                istatistikler.Add(istatistik);
                            }
                            return Ok(istatistikler);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database connection error: {ex.Message}");
            }
        }

        [HttpGet("basvurular")]
        public IActionResult GetBasvurular()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT Musteri,Concat(m.Ad,m.Soyad) as Adsoyad, kt.KrediTuru,Miktar,Vade,BasvuruTarihi,OnayDurumu from KrediBasvuru kb JOIN KrediTurleri kt ON kt.KrediTuruID = kb.KrediTuru\r\nJOIN Musteri m on m.MusteriID = kb.Musteri";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            var basvurular = new List<Dictionary<string, object>>();
                            while (reader.Read())
                            {
                                var basvuru = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    basvuru.Add(reader.GetName(i), reader.GetValue(i));
                                }
                                basvurular.Add(basvuru);
                            }
                            return Ok(basvurular);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }
        public record DateFilter(DateTime BaslangicTarihi, DateTime BitisTarihi);

        [HttpPost("tarihfiltrele")]
        public IActionResult TarihFiltrele([FromBody] DateFilter filtre)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var query = @"SELECT Musteri, 
                     CONCAT(m.Ad, m.Soyad) as AdSoyad,
                     kt.KrediTuru, Miktar, Vade, BasvuruTarihi, OnayDurumu
                     FROM KrediBasvuru kb
                     JOIN KrediTurleri kt ON kt.KrediTuruID = kb.KrediTuru
                     JOIN Musteri m ON m.MusteriID = kb.Musteri
                     WHERE BasvuruTarihi BETWEEN @baslangic AND @bitis";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@baslangic", filtre.BaslangicTarihi);
                cmd.Parameters.AddWithValue("@bitis", filtre.BitisTarihi);

                var basvurular = new List<Dictionary<string, object>>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var basvuru = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        basvuru.Add(reader.GetName(i), reader.GetValue(i));
                    basvurular.Add(basvuru);
                }

                return Ok(basvurular);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }
        public record OnayDurumuFilter(int OnayDurumu);

        [HttpPost("onayfiltrele")]
        public IActionResult OnayDurumuFiltrele([FromBody] OnayDurumuFilter filtre)
        {
            // Gelen değerin geçerli olup olmadığını kontrol et
            if (!IsValidOnayDurumu(filtre.OnayDurumu))
            {
                return BadRequest("Geçersiz onay durumu. Geçerli değerler: 0 (Onaylanmadı), 1 (Onaylandı), 2 (Beklemede)");
            }

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                // Onay durumuna göre açıklayıcı mesaj oluştur
                string durumAciklama = GetOnayDurumuAciklama(filtre.OnayDurumu);

                var query = @"SELECT 
                     CONCAT(m.Ad, ' ', m.Soyad) as AdSoyad, 
                     kt.KrediTuru,
                     kb.Miktar,
                     kb.Vade,
                     kb.BasvuruTarihi,
                     CASE kb.OnayDurumu 
                        WHEN 0 THEN 'Onaylanmadı'
                        WHEN 1 THEN 'Onaylandı'
                        WHEN 2 THEN 'Beklemede'
                     END as OnayDurumu
                     FROM KrediBasvuru kb 
                     JOIN KrediTurleri kt ON kt.KrediTuruID = kb.KrediTuru 
                     JOIN Musteri m ON m.MusteriID = kb.Musteri 
                     WHERE kb.OnayDurumu = @OnayDurumu
                     ORDER BY kb.BasvuruTarihi DESC";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@OnayDurumu", filtre.OnayDurumu);

                var basvurular = new List<Dictionary<string, object>>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var basvuru = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        basvuru.Add(reader.GetName(i), reader.GetValue(i));
                    basvurular.Add(basvuru);
                }

                // Sonuç mesajını oluştur
                var result = new
                {
                    Durum = durumAciklama,
                    ToplamKayit = basvurular.Count,
                    Basvurular = basvurular
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }

        // Onay durumu geçerlilik kontrolü
        private bool IsValidOnayDurumu(int onayDurumu)
        {
            return onayDurumu >= 0 && onayDurumu <= 2;
        }

        // Onay durumu açıklaması
        private string GetOnayDurumuAciklama(int onayDurumu)
        {
            return onayDurumu switch
            {
                0 => "Onaylanmadı",
                1 => "Onaylandı",
                2 => "Beklemede",
                _ => "Bilinmeyen Durum"
            };
        }
        ///         
        public record KrediTuruFilter(String KrediTuru);

        [HttpPost("krediTurufiltrele")]
        public IActionResult KrediTuruFiltrele([FromBody] KrediTuruFilter filtre)
        {
            // Gelen değerin geçerli olup olmadığını kontrol et

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                // Onay durumuna göre açıklayıcı mesaj oluştur

                var query = @"SELECT kt.krediTuru,Count(BasvuruID) as BsvrSys,Sum(Miktar) as Total from KrediBasvuru kb 
                                JOIN KrediTurleri kt on kt.KrediTuruID = kb.KrediTuru 
                                JOIN Musteri m on m.MusteriID = kb.Musteri Where kt.KrediTuru = @KrediTuru Group BY kt.KrediTuru";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@KrediTuru", filtre.KrediTuru);

                var basvurular = new List<Dictionary<string, object>>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var basvuru = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        basvuru.Add(reader.GetName(i), reader.GetValue(i));
                    basvurular.Add(basvuru);
                }

                // Sonuç mesajını oluştur
                var result = new
                {
                    ToplamKayit = basvurular.Count,
                    Basvurular = basvurular
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }

        public record KrediBasvuruFilter(int FirstMiktar,int SecondMiktar);

        [HttpPost("krediBasvurufiltrele")]
        public  IActionResult KrediBasvuruFiltrele([FromBody] KrediBasvuruFilter filtre)

        {
            // Gelen değerin geçerli olup olmadığını kontrol et

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                // Onay durumuna göre açıklayıcı mesaj oluştur

                var query = @"SELECT CONCAT(m.Ad,m.Soyad) as AdSoyad, Musteri, Miktar, BasvuruTarihi,OnayDurumu from 
                                KrediBasvuru kb JOIN KrediTurleri kt on kt.KrediTuruID = kb.KrediTuru JOIN Musteri m on 
                                m.MusteriID= kb.Musteri where Miktar between @FirstMiktar and @SecondMiktar Order by Miktar";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@FirstMiktar", filtre.FirstMiktar);
                cmd.Parameters.AddWithValue("@SecondMiktar", filtre.SecondMiktar);

                var basvurular = new List<Dictionary<string, object>>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var basvuru = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        basvuru.Add(reader.GetName(i), reader.GetValue(i));
                    basvurular.Add(basvuru);
                }

                // Sonuç mesajını oluştur
                var result = new
                {
                    ToplamKayit = basvurular.Count,
                    Basvurular = basvurular
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }

        public record KrediTurTutarFilter(String KrediTuru,int FirstMiktar,int SecondMiktar);
        [HttpPost("krediTurTutarfiltrele")]
        public IActionResult KrediTurTutarFiltrele([FromBody] KrediTurTutarFilter filtre)
        {
            // Gelen değerin geçerli olup olmadığını kontrol et

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                // Onay durumuna göre açıklayıcı mesaj oluştur

                var query = @"SELECT CONCAT(m.Ad,m.Soyad) as AdSoyad, kt.KrediTuru, Miktar, 
                                BasvuruTarihi,OnayDurumu from KrediBasvuru kb JOIN KrediTurleri kt on
                                kt.KrediTuruID = kb.KrediTuru JOIN Musteri m on m.MusteriID= kb.Musteri 
                                where kt.KrediTuru = @KrediTuru and Miktar between @FirstMiktar and @SecondMiktar";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@KrediTuru", filtre.KrediTuru);
                cmd.Parameters.AddWithValue("@FirstMiktar", filtre.FirstMiktar);
                cmd.Parameters.AddWithValue("@SecondMiktar", filtre.SecondMiktar);

                var basvurular = new List<Dictionary<string, object>>();
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var basvuru = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        basvuru.Add(reader.GetName(i), reader.GetValue(i));
                    basvurular.Add(basvuru);
                }

                // Sonuç mesajını oluştur
                var result = new
                {
                    ToplamKayit = basvurular.Count,
                    Basvurular = basvurular
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Hata: {ex.Message}");
            }
        }
        // GET: KrediController
        public ActionResult Index()
        {
            return View();
        }

        // GET: KrediController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: KrediController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: KrediController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: KrediController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: KrediController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: KrediController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: KrediController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
