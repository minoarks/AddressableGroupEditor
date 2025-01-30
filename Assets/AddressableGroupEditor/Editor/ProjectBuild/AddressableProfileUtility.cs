
using UnityEditor.AddressableAssets;

namespace ARK.EditorTools.Addressable
{
    public class AddressableProfileUtility
    {

        public static void SetAddressableProfile(string profileName)
        {
            // 获取Addressable Asset系统的设置
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // 检查是否存在指定的Profile
            if (!settings.profileSettings.GetAllProfileNames().Contains(profileName))
            {
                UnityEngine.Debug.LogError("Profile not found: " + profileName);
                return;
            }

            // 获取Profile的ID并设置为活动Profile
            var profileId = settings.profileSettings.GetProfileId(profileName);
            settings.activeProfileId = profileId;

            UnityEngine.Debug.Log("Addressable Profile set to: " + profileName);
        }        

    }
}