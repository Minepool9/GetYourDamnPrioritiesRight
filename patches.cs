using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Configgy;

[HarmonyPatch(typeof(CameraFrustumTargeter))]
static class CameraFrustumTargeter_Patches
{
    const int MAX_TARGETS = 256;
    const int MAX_OCCLUDERS = 16;
    static readonly Vector3[] corners = new Vector3[4];
    static readonly Collider[] targets = new Collider[MAX_TARGETS];
    static readonly RaycastHit[] occ = new RaycastHit[MAX_OCCLUDERS];
    static readonly Vector2 viewportCenter = new(0.5f, 0.5f);

    static bool IsDeadRecursive(Transform t)
    {
        while (t != null)
        {
            if (t.TryGetComponent<EnemyIdentifier>(out var e) && e.dead)
                return true;
            t = t.parent;
        }
        return false;
    }

    [HarmonyPatch("Update")]
    [HarmonyPrefix]
    static bool Update_Prefix(CameraFrustumTargeter __instance)
    {
        if (!Plugin.modenabled.Value || !CameraFrustumTargeter.isEnabled || __instance.maxHorAim == 0f)
        {
            __instance.CurrentTarget = null;
            __instance.IsAutoAimed = false;
            return false;
        }

        Camera cam = __instance.camera;
        float maxAim = __instance.maxHorAim;

        Ray centerRay = cam.ViewportPointToRay(viewportCenter);
        LayerMask mask = __instance.mask, occMask = __instance.occlusionMask;

        // shoot a ray
        if (Physics.Raycast(centerRay, out var hit, __instance.maximumRange, mask) &&
            !Physics.Raycast(centerRay.origin, centerRay.direction, hit.distance, occMask.value))
        {
            Collider c = hit.collider;
            // coin or grandnaede el boom boom
            bool isCoinOrGrenade = c.TryGetComponent<Coin>(out _) || c.TryGetComponent<Grenade>(out _);
            if ((!c.isTrigger || c.gameObject.layer == 22) && isCoinOrGrenade)
            {
                __instance.CurrentTarget = c;
                __instance.IsAutoAimed = false;
                return false;
            }
        }

        // i just lowk stole this from stack overflow i have half an idea on how this works
        cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), __instance.maximumRange, Camera.MonoOrStereoscopicEye.Mono, corners);
        Bounds bounds = GeometryUtility.CalculateBounds(corners, cam.transform.localToWorldMatrix);
        bounds.size = new Vector3(bounds.size.x, bounds.size.y, __instance.maximumRange);
        bounds.center = __instance.transform.position;

        int found = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, targets, __instance.transform.rotation, mask);
        Collider best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < found; i++)
        {
            Collider col = targets[i];
            if (IsDeadRecursive(col.transform)) continue;

            bool isHelper = col.GetComponent<MassAutoAimHelper>() != null;
            if (isHelper) continue;

            int layer = col.gameObject.layer;
            bool isCoin = layer == 10 && col.TryGetComponent<Coin>(out _);
            bool isGrenade = layer == 14 && col.TryGetComponent<Grenade>(out _);

            if ((col.isTrigger && layer != 22) ||
                (layer == 22 && (!Plugin.hooks.Value || !col.TryGetComponent<HookPoint>(out var hp) || !hp.active)) ||
                (layer == 10 && !isCoin) || (layer == 14 && !isGrenade))
                continue;

            Vector3 dir = col.bounds.center - cam.transform.position;
            float dist = dir.magnitude;
            if (dist == 0f) continue;

            int hits = Physics.RaycastNonAlloc(cam.transform.position, dir, occ, dist, occMask.value, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int j = 0; j < hits; j++)
                if (occ[j].collider != col) { blocked = true; break; }
            if (blocked) continue;

            // turn the target's position into coords inside of the viewport
            Vector3 vp = cam.WorldToViewportPoint(col.bounds.center);

            // mmm yes! coin go ding ding ding!
            float score = isCoin ? -1000f : isGrenade ? -500f : Vector2.SqrMagnitude(new Vector2(vp.x - 0.5f, vp.y - 0.5f));

            // HEY DUDE SO WE FOUND SOMEONE CLOSER HOORAYYYY YOU CAN GO FUCK OFFFFFFF
            if (vp.z >= 0f &&
                vp.x >= .5f - maxAim / 2f && vp.x <= .5f + maxAim / 2f &&
                vp.y >= .5f - maxAim / 2f && vp.y <= .5f + maxAim / 2f &&
                score < bestScore)
            {
                bestScore = score;
                best = col;
            }
        }

        __instance.CurrentTarget = best;
        __instance.IsAutoAimed = best != null;
        return false;
    }

    [HarmonyPatch("LateUpdate")]
    [HarmonyPrefix]
    static bool LateUpdate_Prefix(CameraFrustumTargeter __instance)
    {
        RectTransform cross = __instance.crosshair;
        if (__instance.CurrentTarget == null || !__instance.IsAutoAimed)
        {
            cross.anchoredPosition = Vector2.zero;
            return false;
        }

        Vector3 vp = __instance.camera.WorldToViewportPoint(__instance.CurrentTarget.bounds.center);
        Vector2 offset = new(vp.x - 0.5f, vp.y - 0.5f);
        Vector2 res = cross.GetComponentInParent<Canvas>().GetComponent<CanvasScaler>().referenceResolution;
        cross.anchoredPosition = Vector2.Scale(offset, res);

        return false;
    }
}

// if i only set the layer it doesnt work i had to literally fuckin add a dedicated empty class

[HarmonyPatch(typeof(Mass), "Start")]
static class Mass_Start_Patch
{
    static void Postfix(Mass __instance)
    {
        var cube = __instance.transform.Find("scorpionboss3 (1)/Armature/spine2/Cube");
        if (cube && !cube.GetComponent<MassAutoAimHelper>())
        {
            cube.gameObject.AddComponent<MassAutoAimHelper>();
            cube.gameObject.layer = 10;
        }
    }
}

public class MassAutoAimHelper : MonoBehaviour { }