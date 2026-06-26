using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public class HanoiSessionManager : MonoBehaviour
{
    public static HanoiSessionManager Instance { get; private set; }

    [Header("References")]
    public HanoiManager hanoiManager;

    [Header("Session")]
    public string participantId = "P001";
    public bool isSessionActive = false;

    [Header("Sampling")]
    public float sampleIntervalSec = 0.1f;

    [Header("Hand Anchors")]
    public Transform leftHandAnchor;
    public Transform rightHandAnchor;

    [Header("Grabbed Discs")]
    public Disc currentLeftDisc;
    public Disc currentRightDisc;

    private float sessionStartTime;
    private float lastSampleTime;
    private float timer = 0f;

    private string sessionStartIso;

    private StreamWriter eventWriter;
    private StreamWriter leftWriter;
    private StreamWriter rightWriter;

    // 追加
    private StreamWriter progressWriter;
    private int moveCount = 0;
    private int lastRemainingSteps = -1;
    private Dictionary<string, int> stateVisitCounts = new Dictionary<string, int>();

    private Vector3 prevLPos, prevRPos;
    private Quaternion prevLRot, prevRRot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (hanoiManager == null)
            hanoiManager = FindObjectOfType<HanoiManager>();
    }

    void Update()
    {
        if (!isSessionActive) return;

        timer += Time.deltaTime;
        if (timer >= sampleIntervalSec)
        {
            SampleTick();
            timer -= sampleIntervalSec;
        }
    }

    void OnDestroy()
    {
        if (isSessionActive) EndSession();
    }

    public void StartSession()
    {
        if (isSessionActive) return;

        if (hanoiManager != null)
            hanoiManager.ResetGame();

        isSessionActive = true;
        sessionStartTime = Time.time;
        lastSampleTime = sessionStartTime;
        timer = 0f;

        moveCount = 0;
        lastRemainingSteps = -1;
        stateVisitCounts.Clear();

        sessionStartIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

        OpenCsvFiles();

        WriteEvent("SESSION_START", -1, "", "", $"start={sessionStartIso}");

        if (leftHandAnchor != null)
        {
            prevLPos = leftHandAnchor.position;
            prevLRot = leftHandAnchor.rotation;
        }
        if (rightHandAnchor != null)
        {
            prevRPos = rightHandAnchor.position;
            prevRRot = rightHandAnchor.rotation;
        }

        // 初期状態の進捗を保存
        WriteProgressSnapshot("SESSION_START");

        Debug.Log("[Session] Start");
    }

    public void EndSession()
    {
        if (!isSessionActive) return;

        // 終了時点の進捗も保存
        WriteProgressSnapshot("SESSION_END");

        float t = Time.time - sessionStartTime;
        WriteEvent("SESSION_END", -1, "", "", $"duration_s={t:F3}");

        isSessionActive = false;

        CloseCsvFiles();

        if (hanoiManager != null)
            hanoiManager.ResetGame();

        Debug.Log("[Session] End");
    }

    public void LogEvent(string eventName, Disc disc = null, Tower from = null, Tower to = null, string extra = "")
    {
        if (!isSessionActive) return;

        int size = disc != null ? disc.sizeIndex : -1;
        string fromName = from != null ? from.name : "";
        string toName = to != null ? to.name : "";

        WriteEvent(eventName, size, fromName, toName, extra);

        // 合法的にディスクを置けたタイミングで進捗を書く
        // eventName はあなたの既存イベント名に合わせてください
        if (eventName == "MOVE_SUCCESS" || eventName == "DISC_PLACED")
        {
            moveCount++;
            WriteProgressSnapshot(eventName);
        }

        // ヒント表示時も残したければここで追加可能
        if (eventName == "HINT_SHOW")
        {
            WriteProgressSnapshot(eventName);
        }
    }

    public void SetGrabbedDisc(bool isLeftHand, Disc discOrNull)
    {
        if (isLeftHand) currentLeftDisc = discOrNull;
        else currentRightDisc = discOrNull;
    }

    private void SampleTick()
    {
        float now = Time.time;
        float timeS = now - sessionStartTime;
        float dt = now - lastSampleTime;
        if (dt <= 0f) dt = sampleIntervalSec;
        lastSampleTime = now;

        if (leftHandAnchor != null)
        {
            WriteSampleForHand(
                leftWriter,
                "L",
                leftHandAnchor,
                ref prevLPos,
                ref prevLRot,
                dt,
                timeS,
                currentLeftDisc
            );
        }

        if (rightHandAnchor != null)
        {
            WriteSampleForHand(
                rightWriter,
                "R",
                rightHandAnchor,
                ref prevRPos,
                ref prevRRot,
                dt,
                timeS,
                currentRightDisc
            );
        }

        leftWriter?.Flush();
        rightWriter?.Flush();
    }

    private void WriteSampleForHand(
        StreamWriter writer,
        string handLabel,
        Transform handTf,
        ref Vector3 prevPos,
        ref Quaternion prevRot,
        float dt,
        float timeS,
        Disc grabbedDisc
    )
    {
        if (writer == null) return;

        Vector3 pos = handTf.position;
        Vector3 euler = handTf.rotation.eulerAngles;

        Vector3 v = (pos - prevPos) / dt;

        Quaternion dq = handTf.rotation * Quaternion.Inverse(prevRot);
        dq.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        Vector3 w = (axis.sqrMagnitude < 1e-8f) ? Vector3.zero : (axis.normalized * angleDeg) / dt;

        prevPos = pos;
        prevRot = handTf.rotation;

        int discSize = -1;
        Vector3 discPos = Vector3.zero;
        bool hasDisc = grabbedDisc != null;
        if (hasDisc)
        {
            discSize = grabbedDisc.sizeIndex;
            discPos = grabbedDisc.transform.position;
        }

        string line =
            $"{participantId},{sessionStartIso},{timeS:F3},{dt:F3},{handLabel}," +
            $"{pos.x:F4},{pos.y:F4},{pos.z:F4}," +
            $"{euler.x:F2},{euler.y:F2},{euler.z:F2}," +
            $"{v.x:F4},{v.y:F4},{v.z:F4}," +
            $"{w.x:F2},{w.y:F2},{w.z:F2}," +
            $"{discSize}," +
            $"{(hasDisc ? discPos.x.ToString("F4") : "")}," +
            $"{(hasDisc ? discPos.y.ToString("F4") : "")}," +
            $"{(hasDisc ? discPos.z.ToString("F4") : "")}";

        writer.WriteLine(line);
    }

    private void OpenCsvFiles()
    {
        string safeIso = sessionStartIso.Replace(":", "");
        string baseDir = Application.persistentDataPath;

        string eventPath = Path.Combine(baseDir, $"hanoi_{participantId}_{safeIso}_events.csv");
        string leftPath = Path.Combine(baseDir, $"hanoi_{participantId}_{safeIso}_left.csv");
        string rightPath = Path.Combine(baseDir, $"hanoi_{participantId}_{safeIso}_right.csv");
        string progressPath = Path.Combine(baseDir, $"hanoi_{participantId}_{safeIso}_progress.csv");

        eventWriter = new StreamWriter(eventPath, false, new UTF8Encoding(false));
        leftWriter = new StreamWriter(leftPath, false, new UTF8Encoding(false));
        rightWriter = new StreamWriter(rightPath, false, new UTF8Encoding(false));
        progressWriter = new StreamWriter(progressPath, false, new UTF8Encoding(false));

        eventWriter.WriteLine("participant_id,session_start_iso,event,time_s,disc_size,from_tower,to_tower,extra");
        leftWriter.WriteLine("participant_id,session_start_iso,time_s,dt,hand,hand_pos_x,hand_pos_y,hand_pos_z,hand_rot_x,hand_rot_y,hand_rot_z,hand_v_x,hand_v_y,hand_v_z,hand_w_x,hand_w_y,hand_w_z,disc_size,disc_pos_x,disc_pos_y,disc_pos_z");
        rightWriter.WriteLine("participant_id,session_start_iso,time_s,dt,hand,hand_pos_x,hand_pos_y,hand_pos_z,hand_rot_x,hand_rot_y,hand_rot_z,hand_v_x,hand_v_y,hand_v_z,hand_w_x,hand_w_y,hand_w_z,disc_size,disc_pos_x,disc_pos_y,disc_pos_z");
        progressWriter.WriteLine("participant_id,session_start_iso,time_s,move_count,state_id,remaining_optimal_steps,progress_rate,progressed_flag,regressed_flag,revisit_count,is_solved,note");
    }

    private void CloseCsvFiles()
    {
        eventWriter?.Flush();
        eventWriter?.Close();
        eventWriter = null;

        leftWriter?.Flush();
        leftWriter?.Close();
        leftWriter = null;

        rightWriter?.Flush();
        rightWriter?.Close();
        rightWriter = null;

        progressWriter?.Flush();
        progressWriter?.Close();
        progressWriter = null;
    }

    private void WriteEvent(string eventName, int discSize, string fromTower, string toTower, string extra)
    {
        if (eventWriter == null) return;

        float timeS = Time.time - sessionStartTime;
        extra = (extra ?? "").Replace(",", ";").Replace("\n", " ").Replace("\r", " ");

        string line = $"{participantId},{sessionStartIso},{eventName},{timeS:F3},{discSize},{fromTower},{toTower},{extra}";
        eventWriter.WriteLine(line);
        eventWriter.Flush();
    }

    private void WriteProgressSnapshot(string note)
    {
        if (progressWriter == null || hanoiManager == null) return;

        int[] rodState = hanoiManager.GetCurrentRodState();
        if (rodState == null || rodState.Length == 0) return;

        string stateId = string.Join("-", rodState);

        int remaining = GetRemainingStepsToGoal(
            rodState,
            hanoiManager.TotalDiscs,
            hanoiManager.goalTowerIndex
        );

        int totalOptimalSteps = (1 << hanoiManager.TotalDiscs) - 1;
        float progressRate = 1f - ((float)remaining / totalOptimalSteps);

        int progressedFlag = 0;
        int regressedFlag = 0;

        if (lastRemainingSteps >= 0)
        {
            if (remaining < lastRemainingSteps) progressedFlag = 1;
            else if (remaining > lastRemainingSteps) regressedFlag = 1;
        }

        lastRemainingSteps = remaining;

        if (!stateVisitCounts.ContainsKey(stateId))
            stateVisitCounts[stateId] = 0;
        stateVisitCounts[stateId]++;

        int revisitCount = stateVisitCounts[stateId] - 1;
        int isSolved = remaining == 0 ? 1 : 0;

        float timeS = Time.time - sessionStartTime;

        string safeNote = (note ?? "").Replace(",", ";").Replace("\n", " ").Replace("\r", " ");

        string line =
            $"{participantId},{sessionStartIso},{timeS:F3},{moveCount}," +
            $"{stateId},{remaining},{progressRate:F4},{progressedFlag},{regressedFlag},{revisitCount},{isSolved},{safeNote}";

        progressWriter.WriteLine(line);
        progressWriter.Flush();
    }

    // rodState[i] = ディスク i が今どの棒にあるか
    // i は小さいディスクから順番でも大きいディスクから順番でもよいですが、
    // 下の実装では「sizeIndex が小さいほど小さいディスク」を想定しています。
    private int GetRemainingStepsToGoal(int[] rodState, int n, int targetRod)
    {
        if (n == 0) return 0;

        int currentRod = rodState[n - 1];

        if (currentRod == targetRod)
        {
            return GetRemainingStepsToGoal(rodState, n - 1, targetRod);
        }
        else
        {
            int auxRod = 3 - currentRod - targetRod;

            return GetRemainingStepsToGoal(rodState, n - 1, auxRod)
                   + 1
                   + ((1 << (n - 1)) - 1);
        }
    }
}