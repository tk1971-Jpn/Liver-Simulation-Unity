using UnityEngine;
using UnityEngine.InputSystem;

public class ToolController : MonoBehaviour
{
    [Header("Settings")]
    public Camera mainCamera;
    public LayerMask targetLayer; // 肝臓のレイヤーを指定
    public float offsetFromSurface = 0.001f; // 表面からわずかに浮かす

    void Update()
    {
        // 1. マウスの位置から画面の奥に向かって「光線(Ray)」を飛ばす
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit hit;

        // 2. 光線が物体（肝臓など）に当たったか判定
        if (Physics.Raycast(ray, out hit, 100f, targetLayer))
        {
            // 3. 当たった場所に器具を移動
            transform.position = hit.point + (hit.normal * offsetFromSurface);

            // 4. 器具を表面の向き（法線）に合わせて立たせる
            transform.up = hit.normal;
        }
    }
}