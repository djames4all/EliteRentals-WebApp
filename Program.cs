using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EliteRentals.Services;
using System.Globalization;

namespace EliteRentals
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ----------------------------
            // Services
            // ----------------------------

            // Session backing store
            builder.Services.AddDistributedMemoryCache();

            // Named HttpClient for your MVC API (reads ApiSettings:BaseUrl)
            var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "";
            builder.Services.AddHttpClient("EliteRentalsAPI", client =>
            {
                if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                {
                    client.BaseAddress = new Uri(apiBaseUrl);
                }
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });

            // Typed API service (you'll add EliteApi.cs)
            builder.Services.AddScoped<IEliteApi, EliteApi>();

            // Authentication (Cookies + Google)
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
                    // Capture id_token if present
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

            // Session (single, de-duplicated)
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            builder.Services.AddHttpClient();         // generic factory (ok to keep)
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<EmailService>();


            var app = builder.Build();

            // ----------------------------
            // Middleware pipeline
            // ----------------------------
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage(); // verbose errors in dev
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Session before auth if you rely on it during auth
            app.UseSession();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapRazorPages();
            
            var culture = CultureInfo.GetCultureInfo("en-ZA");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            app.Run();
        }
    }
}
