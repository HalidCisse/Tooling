using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Tooling.KeyVaulter
{
    public class Program
    {
        private static async Task Main()
        {
            var purgeExisting = false;
            var configs = new ConfigurationBuilder()
                         .AddUserSecrets(Assembly.GetEntryAssembly())
                         .Build();

            var akvBaseUrl = Environment.GetEnvironmentVariable("KeyVault_BaseUrl", EnvironmentVariableTarget.Process);
            var akvClientId = Environment.GetEnvironmentVariable("KeyVault_ClientId", EnvironmentVariableTarget.Process);
            var akvClientSecret = Environment.GetEnvironmentVariable("KeyVault_ClientSecret", EnvironmentVariableTarget.Process);

            var kvClient = new KeyVaultClient(async (authority, resource, _)
                                                  => (await new AuthenticationContext(authority, null)
                                                      .AcquireTokenAsync(resource, new ClientCredential(
                                                                             akvClientId,
                                                                             akvClientSecret))).AccessToken);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (purgeExisting)
            {
                Console.WriteLine("###################### PURGING SECRETS  ##########################");
                var secretItems = await kvClient.GetSecretsAsync(akvBaseUrl);

                async Task PurgeSecret(SecretItem secretItem)
                {
                    try
                    {
                        Console.WriteLine($@"{nameof(PurgeSecret)} :: {secretItem.Identifier.Name}");
                        await kvClient.DeleteSecretAsync(akvBaseUrl, secretItem.Identifier.Name);
                        try
                        {
                            await kvClient.DeleteSecretAsync(akvBaseUrl, secretItem.Identifier.Name);
                        }
                        catch
                        {
                            /*ignored*/
                        }
                    }
                    catch (KeyVaultErrorException e)
                    {
                        Console.WriteLine(e);
                    }
                }
                await ForEach(secretItems, async secretItem => await PurgeSecret(secretItem), maxThreadCount: 10);

                while (!string.IsNullOrWhiteSpace(secretItems.NextPageLink))
                {
                    secretItems = await kvClient.GetSecretsNextAsync(secretItems.NextPageLink);
                    foreach (var secretItem in secretItems)
                        await PurgeSecret(secretItem);
                }

                Console.WriteLine("###################### DONE PURGING SECRETS  ##########################");
            }


            Console.WriteLine("###################### ADDING NEW VALUES ##########################");

            await ForEach(configs.AsEnumerable(true), async pair =>
            {
                var (key, value) = pair;
                var vaultnizedKey = key.Replace(":", "--").Trim();

                if (string.IsNullOrWhiteSpace(value))
                    return;

                try
                {
                    Console.WriteLine($@"Adding :: {vaultnizedKey} -> {value}");
                    await kvClient.SetSecretAsync(akvBaseUrl, vaultnizedKey, value);
                }
                catch (KeyVaultErrorException e)
                {
                    Console.WriteLine(e);
                }
            }, maxThreadCount: 10);


            static async Task ForEach<T>(IEnumerable<T> source, Func<T, Task> body, Action<T, Exception> onError = default, int maxThreadCount = 10)
            {
                var guard = new SemaphoreSlim(maxThreadCount);
                await Task.WhenAll(
                        source
                            .Select(async arg =>
                            {
                                try
                                {
                                    await guard.WaitAsync();
                                    await body(arg);
                                }
                                catch (Exception e)
                                {
                                    onError?.Invoke(arg, e);
                                }
                                finally
                                {
                                    guard.Release();
                                }
                            }));
                guard.Dispose();
            }

            await Task.Delay(1_000);
            Console.WriteLine("###################### DONE ##########################");
        }
    }
}