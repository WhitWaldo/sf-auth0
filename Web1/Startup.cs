using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Logging;

namespace Web1
{
	public class Startup
	{
		private readonly StatelessServiceContext _context;

		public Startup(StatelessServiceContext context)
		{
			_context = context;
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddRazorPages();
			services.AddServerSideBlazor();

			services.Configure<CookiePolicyOptions>(options =>
			{
				options.CheckConsentNeeded = context => true;
				options.MinimumSameSitePolicy = SameSiteMode.None;
			});

			services.AddAuthentication(options =>
				{
					options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
					options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
					options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
				})
				.AddCookie()
				.AddOpenIdConnect("Auth0", options =>
				{
					var domain = _context.CodePackageActivationContext.GetConfigurationPackageObject("Config")
						.Settings.Sections["Auth0"].Parameters["Instance"].Value;
					;
					var clientId = _context.CodePackageActivationContext.GetConfigurationPackageObject("Config")
						.Settings.Sections["Auth0"].Parameters["ClientId"].Value;

					options.Authority = domain;
					options.ClientId = clientId;
					options.ClientSecret = _context.CodePackageActivationContext.GetConfigurationPackageObject("Config")
						.Settings.Sections["Auth0"].Parameters["ClientSecret"].Value;

					// Set response type to code
					options.ResponseType = "code";

					// Configure the scope
					options.Scope.Clear();
					options.Scope.Add("openid");

					// Set the callback path, so Auth0 will call back to http://localhost:3000/callback
					// Also ensure that you have added the URL as an Allowed Callback URL in your Auth0 dashboard
					options.CallbackPath = new PathString("/callback");

					// Configure the Claims Issuer to be Auth0
					options.ClaimsIssuer = "Auth0";

					options.Events = new OpenIdConnectEvents
					{
						OnRedirectToIdentityProviderForSignOut = (context) =>
						{
							var logoutUri = $"{domain}/v2/logout?client_id={clientId}";

							var postLogoutUri = context.Properties.RedirectUri;
							if (!string.IsNullOrEmpty(postLogoutUri))
							{
								if (postLogoutUri.StartsWith("/"))
								{
									// transform to absolute
									var request = context.Request;
									postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase +
									                postLogoutUri;
								}

								logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
							}

							context.Response.Redirect(logoutUri);
							context.HandleResponse();

							return Task.CompletedTask;
						}
					};
				});

			IdentityModelEventSource.ShowPII = true;

			services.AddHttpContextAccessor();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();

			app.UseRouting();

			app.UseCookiePolicy();
			app.UseAuthentication();
			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapBlazorHub();
				endpoints.MapFallbackToPage("/_Host");
			});
		}
	}
}
