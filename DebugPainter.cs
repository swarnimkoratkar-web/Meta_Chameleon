using UnityEngine;
using UnityEngine.InputSystem;

public class DebugPainter : MonoBehaviour
{
    public Camera cam;
    public int brushSize = 8;
    public Color paintColor = Color.cyan;

    private void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            Vector2 mousePos = Pointer.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);

            int layerMask = LayerMask.GetMask("PlayerBody");

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask))
            {
                MeshCollider meshCollider = hit.collider as MeshCollider;
                Renderer rend = hit.collider.GetComponent<Renderer>();

                if (rend == null || rend.material == null || meshCollider == null) return;

                Texture2D tex = rend.material.GetTexture("_BaseMap") as Texture2D;

                if (tex != null && !tex.name.EndsWith("(RuntimeClone)"))
                {
                    Texture2D runtimeTex = new Texture2D(tex.width, tex.height);
                    runtimeTex.name = tex.name + "(RuntimeClone)";
                    runtimeTex.SetPixels(tex.GetPixels());
                    runtimeTex.Apply();

                    rend.material.SetTexture("_BaseMap", runtimeTex);
                    tex = runtimeTex;
                }

                if (tex == null) return;

                Vector2 pixelUV = hit.textureCoord;
                int x = (int)(pixelUV.x * tex.width);
                int y = (int)(pixelUV.y * tex.height);

                for (int i = -brushSize; i < brushSize; i++)
                {
                    for (int j = -brushSize; j < brushSize; j++)
                    {
                        int px = Mathf.Clamp(x + i, 0, tex.width - 1);
                        int py = Mathf.Clamp(y + j, 0, tex.height - 1);
                        tex.SetPixel(px, py, paintColor);
                    }
                }
                tex.Apply();
            }
        }
    }
}