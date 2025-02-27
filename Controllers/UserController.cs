using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.AspNetCore.Identity.Data;


namespace deneme.Controllers
{

    [ApiController]
    [Route("api/[controller]")]

    public class UserController : Controller
    {
        private readonly IConfiguration _configuration;

        public UserController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

     [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest("Kullanıcı adı ve şifre gereklidir.");
                }
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(UserId) FROM [Users] WHERE UserName=@Username AND Password=@Password AND Status=1";
                    // Önce veritabanındaki tabloları listeleyelim
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Username", request.Username);
                        cmd.Parameters.AddWithValue("@Password", request.Password);

                        int userCount = (int)cmd.ExecuteScalar();

                        if (userCount > 0)
                        {
                            return Ok(new { success = true, message = "Giriş başarılı." });
                        }
                        else
                        {
                            return Unauthorized(new { success = false, message = "Kullanıcı adı veya şifre hatalı!" });
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database connection error: {ex.Message}");
            }
        }
    }

}

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}