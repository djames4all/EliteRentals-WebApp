
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace EliteRentals
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);



            // Add session backing store
            builder.Services.AddDistributedMemoryCache();

            builder.Services.AddHttpClient("EliteRentalsAPI", client =>
            {
                client.BaseAddress = new Uri("https://localhost:7196/"); 
            });

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie()
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.SaveTokens = true;

    options.Scope.Add("openid");
    options.Scope.Add("email");
    options.Scope.Add("profile");

    options.Events.OnCreatingTicket = ctx =>
    {
        // Try to capture the id_token from the response
        if (ctx.Properties?.GetTokens() is List<AuthenticationToken> tokens)
        {
            var idToken = ctx.TokenResponse?.Response?.RootElement
                .GetProperty("id_token").GetString();

            if (!string.IsNullOrEmpty(idToken))
            {
                tokens.Add(new AuthenticationToken { Name = "id_token", Value = idToken });
                ctx.Properties.StoreTokens(tokens);
            }
        }

        return Task.CompletedTask;
    };
});





            builder.Services.AddSession();

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Add session services
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpClient();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddRazorPages();

            var app = builder.Build();

            // Middleware pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Session must come before auth if you're using it for login state
            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapRazorPages();

            app.Run();
        }
    }
}
