using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 총책임자(조율자) 판단 기록 (7단계 RL 재학습용).
/// 지시를 내리는 순간마다 실제 관측 23개 값, 내린 지시, 탐험 여부, RL Q 값, 파티 상태를 CSV 한 줄로 남김.
/// 학습 데이터를 그래프 기록에서 다시 계산하지 않고 실행 시점 관측 그대로 쓰기 위한 기록
/// (그래프 기록 재구성 관측과 실행 관측의 불일치 문제 대응).
/// </summary>
public static class CoordinatorDecisionLog
{
    private static StreamWriter writer;
    private static string currentPath;
    private static readonly StringBuilder line = new StringBuilder(512);

    public static void Write(string root, string fileName, string episodeId, string algorithm, CoordinatorMode mode,
        NPCSimpleFSMController npc, float[] obs, NpcCommandType command, bool explored, float[] qValues,
        int partyAlive, int partyTotal)
    {
        try
        {
            string folder = string.IsNullOrWhiteSpace(root) ? Application.persistentDataPath : root;
            string path = Path.Combine(folder, fileName);
            if (writer == null || currentPath != path)
            {
                Close();
                Directory.CreateDirectory(folder);
                bool isNew = !File.Exists(path);
                writer = new StreamWriter(path, true, new UTF8Encoding(false));
                currentPath = path;
                if (isNew)
                    writer.WriteLine(Header());
            }

            CultureInfo ci = CultureInfo.InvariantCulture;
            line.Clear();
            line.Append(episodeId).Append(',').Append(algorithm).Append(',').Append(mode).Append(',');
            line.Append(Time.timeSinceLevelLoad.ToString("0.000", ci)).Append(',');
            line.Append(npc != null ? npc.name.Replace(',', '_') : "").Append(',');
            line.Append(npc != null && npc.IsHealerRole ? 1 : 0).Append(',');
            line.Append(npc != null && npc.IsTankRole ? 1 : 0).Append(',');
            for (int i = 0; i < CoordinatorObservation.Dim; i++)
                line.Append(obs[i].ToString("0.#####", ci)).Append(',');
            line.Append((int)command).Append(',').Append(explored ? 1 : 0).Append(',');
            for (int i = 0; i < 5; i++)
                line.Append(qValues != null && i < qValues.Length ? qValues[i].ToString("0.#####", ci) : "").Append(',');
            line.Append(partyAlive).Append(',').Append(partyTotal);
            writer.WriteLine(line.ToString());
            writer.Flush();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CoordinatorDecisionLog] 기록 실패: {e.Message}");
        }
    }

    public static void Close()
    {
        writer?.Dispose();
        writer = null;
        currentPath = null;
    }

    private static string Header()
    {
        StringBuilder h = new StringBuilder("episode_id,algorithm,mode,t,npc,is_healer_role,is_tank_role,");
        for (int i = 0; i < CoordinatorObservation.Dim; i++)
            h.Append("obs").Append(i).Append(',');
        h.Append("command,explored,q0,q1,q2,q3,q4,party_alive,party_total");
        return h.ToString();
    }
}
