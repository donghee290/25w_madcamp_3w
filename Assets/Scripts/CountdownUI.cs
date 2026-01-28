using TMPro;
using UnityEngine;

public class CountdownUI : MonoBehaviour
{
    public TextMeshProUGUI text;

    void Awake()
    {
        if (text == null) text = GetComponent<TextMeshProUGUI>();
        Hide();
    }

    public void SetNumber(int n)
    {
        if (text == null) return;
        text.text = n.ToString();
        text.enabled = true;
    }

    public void SetGo()
    {
        if (text == null) return;
        text.text = "GO!";
        text.enabled = true;
    }

    public void Hide()
    {
        if (text == null) return;
        text.enabled = false;
    }
}
