using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace active_directory_netcore_interactive_v2
{
    public class Program
    {
        // Register an app and configure http://localhost redirect uri 
        private const string ClientId = "1d18b3b0-251b-4714-a02a-9956cec86c2d";

        // Since the browser is started via Process.Start, there is no control over it
        // so configure a timeout 
        private const int TimeoutWaitingForBrowserMs = 5 * 60 * 1000; 

        // Scopes for which an access token is requested
        private static readonly IEnumerable<string> s_scopes = new[] { "user.read" }; 
        private const string GraphAPIEndpoint = "https://graph.microsoft.com/v1.0/me";

        private void MyLoggingMethod(LogLevel level, string message, bool containsPii)
        {
            Console.WriteLine($"MSAL {level} {containsPii} {message}");
        }
        
        static void Main(string[] args)
        {
            FetchTokenAndCallProtectedApiAsync().GetAwaiter().GetResult();
        }

        private static async Task FetchTokenAndCallProtectedApiAsync()
        {
            IPublicClientApplication pca = PublicClientApplicationBuilder
                           .Create(ClientId)
                           .WithRedirectUri(DefaultOsBrowserWebUi.FindFreeLocalhostRedirectUri()) // required for DefaultOsBrowser
                           .WithLogging(MyLoggingMethod, LogLevel.Info,
                               enablePiiLogging: true, 
                               enableDefaultPlatformLogging: true)
                           .Build();

            AuthenticationResult authResult = await FetchTokenFromCacheAsync(pca).ConfigureAwait(false);
            if (authResult == null)
            {
                authResult = await FetchTokenInteractivelyAsync(pca).ConfigureAwait(false);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Token acquired for: " + authResult.Account.Username);
            Console.ResetColor();

            // Now use the token to access a protected API
            await CallGraphAsync(authResult.AccessToken).ConfigureAwait(false);
        }

        private static async Task<AuthenticationResult> FetchTokenFromCacheAsync(IPublicClientApplication pca)
        {
            try
            {
                var accounts = await pca.GetAccountsAsync().ConfigureAwait(false);

                return await pca.AcquireTokenSilent(s_scopes, accounts.FirstOrDefault())
                 .ExecuteAsync()
                 .ConfigureAwait(false);
            }
            catch (MsalUiRequiredException ex)
            {
                return null;
            }
        }

        private static async Task<AuthenticationResult> FetchTokenInteractivelyAsync(IPublicClientApplication pca)
        {
            try
            {
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeoutWaitingForBrowserMs);

                return await pca.AcquireTokenInteractive(s_scopes)
                  .WithCustomWebUi(new DefaultOsBrowserWebUi())
                  .ExecuteAsync(cancellationTokenSource.Token)
                  .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to acquire a token interactively... ");
                Console.WriteLine(ex.Message);
                Console.ResetColor();

                throw;
            }
        }
        private static async Task CallGraphAsync(string token)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, GraphAPIEndpoint);
                //Add the token in Authorization header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request).ConfigureAwait(false);
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Result from calling the ME endpoint of the graph: " + content);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to call the graph ... ");
                Console.WriteLine(ex.Message);
                Console.ResetColor();

                throw;
            }
        }
    }
}
