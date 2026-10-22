using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class NetworkInitializer : MonoBehaviour
{
    async void Start()
    {
        // 🌐 Connects your game build securely to the backend services
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}