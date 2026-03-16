using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuManager : MonoBehaviour
{
    public TMP_Dropdown resolutionDropdown;
    public Slider volumeSlider;
    public Image cardBackPreview;
    public Sprite[] cardBacks;
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    private int currentBackIndex = 0;

    private void Start()
    {
        InitializeResolutions();
        //InitializeVolume();
    }



    private void InitializeResolutions()
    {
        if (resolutionDropdown == null)
        {
            Debug.LogError("resolutionDropdown не назначен!");
            return;
        }

        List<string> resolutions = new List<string>();
        foreach (var res in Screen.resolutions)
        {
            resolutions.Add(res.width + "x" + res.height);
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(resolutions);

        string currentRes = Screen.currentResolution.width + "x" + Screen.currentResolution.height;
        int defaultIndex = resolutions.IndexOf(currentRes);
        resolutionDropdown.value = defaultIndex >= 0 ? defaultIndex : resolutions.Count - 1;
    }

    public void SetResolution(int index)
    {
        string selected = resolutionDropdown.options[index].text;
        string[] parts = selected.Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
        {
            Screen.SetResolution(width, height, Screen.fullScreen);
        }
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    public void ChangeCardBack()
    {
        currentBackIndex = (currentBackIndex + 1) % cardBacks.Length;
        cardBackPreview.sprite = cardBacks[currentBackIndex];
    }

    public void BackToMain()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}