using UnityEngine;
using UnityEngine.UI;

public class ChaseGaugeVisual : MonoBehaviour
{
    [Header("Refs")]
    public ChaserSystem chaser;
    public RectTransform trackArea;       // TrackArea
    public Image fillGradient;            // FillGradient Image
    public RectTransform playerIcon;      // PlayerIcon
    public RectTransform chaserIcon;      // ChaserIcon

    [Header("Icon tuning")]
    public float iconPadLeft = 0f;
    public float iconPadRight = 0f;

    [Header("Gradient (Close->Far)")]
    public Color closeColor = new Color(0.95f, 0.25f, 0.25f, 1f); // 빨강(가까움)
    public Color farColor   = new Color(0.45f, 0.90f, 0.55f, 1f); // 연두(멀음)
    [Range(32, 512)] public int gradientWidth = 256;

    Sprite _gradSprite;

    void Awake()
    {
        if (!chaser) chaser = FindFirstObjectByType<ChaserSystem>();

        // 그라데이션 스프라이트 생성 + Filled 세팅 강제
        if (fillGradient)
        {
            _gradSprite = CreateGradientSprite(closeColor, farColor, gradientWidth, 1);
            fillGradient.sprite = _gradSprite;
            fillGradient.type = Image.Type.Filled;
            fillGradient.fillMethod = Image.FillMethod.Horizontal;
            fillGradient.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillGradient.color = Color.white;
            fillGradient.preserveAspect = false;
        }
    }

    void LateUpdate()
    {
        if (!trackArea || !fillGradient || !playerIcon || !chaserIcon) return;

        float ratio = 0f;
        if (chaser && chaser.maxDistance > 0.0001f)
            ratio = Mathf.Clamp01(chaser.chaserDistance / chaser.maxDistance);
        // ratio=1 멀다(안전) / ratio=0 가깝다(위험)

        // 1) Fill은 ratio만큼 채우기 (멀수록 많이 채워짐)
        fillGradient.fillAmount = ratio;

        // 2) 아이콘 위치 (TrackArea 기준: 왼쪽 0 ~ 오른쪽 w)
        float w = trackArea.rect.width;
        float iconHalf = playerIcon.rect.width * 0.5f;

        float left = iconHalf + iconPadLeft;
        float right = w - iconHalf - iconPadRight;

        SetX(playerIcon, left);
        SetX(chaserIcon, Mathf.Lerp(left, right, ratio));
    }

    void SetX(RectTransform rt, float x)
    {
        // 아이콘 앵커는 (0,0.5)로 고정돼 있어야 함
        var p = rt.anchoredPosition;
        p.x = x;
        rt.anchoredPosition = p;
    }

    static Sprite CreateGradientSprite(Color left, Color right, int width, int height)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < width; x++)
        {
            float t = (width <= 1) ? 0f : (float)x / (width - 1);
            Color c = Color.Lerp(left, right, t);
            for (int y = 0; y < height; y++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}