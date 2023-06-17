namespace WebAPI
{

    public class ApiStarter
    {
        public static void Main(int port)
        {
            var builder = WebApplication.CreateBuilder(new string[0]);

            // Add services to the container.

            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseAuthorization();

            app.MapControllers();

            //app.Run($"http://0.0.0.0:{port}/");
        }
    }

}


