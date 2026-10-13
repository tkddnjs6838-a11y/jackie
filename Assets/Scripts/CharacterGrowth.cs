using UnityEngine;

// Run-local augments; scene reload resets them. Temporary buffs stay separate.
public sealed class CharacterGrowth : MonoBehaviour
{
    // Keep existing serialized pickup values stable.
    public enum Kind { Power, Speed, Vitality, QuickHands, LongReach, WideSwing, BloodEdge, SecondWind }
    public const int MaxLevel=5;
    readonly int[] levels=new int[8];
    public int Level(Kind kind)=>(int)kind>=0 && (int)kind<levels.Length?levels[(int)kind]:0;
    public static int Limit(Kind kind)=>kind==Kind.BloodEdge?3:MaxLevel;
    public float DamageMultiplier=>1f+Level(Kind.Power)*.1f;
    public float MoveMultiplier=>1f+Level(Kind.Speed)*.05f;
    public float CooldownMultiplier=>Mathf.Pow(.85f,Level(Kind.QuickHands));
    public bool CanCollect(Kind kind)
    {
        if((int)kind<0 || (int)kind>=levels.Length || Level(kind)>=Limit(kind))return false;
        var attack=GetComponent<PlayerAutoAttack>();
        switch(kind)
        {
            case Kind.LongReach:return attack!=null && attack.manualAttackRange<6f;
            case Kind.WideSwing:return attack!=null && attack.attackAngle<300f;
            case Kind.BloodEdge:return attack!=null && attack.lifestealPerHit<9f;
            case Kind.SecondWind:return GetComponent<PlayerDash>() is PlayerDash dash && dash.cooldown>.5f;
            default:return true;
        }
    }
    public bool TryCollect(Kind kind)
    {
        if(Time.timeScale<=0)return false;
        return TryApplyChoice(kind);
    }
    internal bool TryApplyChoice(Kind kind)
    {
        var health=GetComponent<PlayerHealth>();
        if(RunStats.Instance==null || !RunStats.Instance.Running || health==null || health.IsDead || !CanCollect(kind))return false;
        levels[(int)kind]++;
        var attack=GetComponent<PlayerAutoAttack>();
        switch(kind)
        {
            case Kind.Vitality:health.AddMaxHealth(15);break;
            case Kind.LongReach:attack.manualAttackRange=Mathf.Min(6f,attack.manualAttackRange+.4f);break;
            case Kind.WideSwing:attack.attackAngle=Mathf.Min(300f,attack.attackAngle+25f);break;
            case Kind.BloodEdge:
                attack.lifestealPerHit=Mathf.Min(9f,attack.lifestealPerHit+3f);
                attack.lifestealPerSecondCap+=1.5f;break;
            case Kind.SecondWind:
                var dash=GetComponent<PlayerDash>();dash.cooldown=Mathf.Max(.5f,dash.cooldown-.4f);break;
        }
        GameAudio.Play(GameAudio.Sfx.UpgradePick);
        DamageNumber.Create(transform.position+Vector3.up*1.5f,Name(kind)+" "+Level(kind)+"/"+Limit(kind),ColorFor(kind));
        return true;
    }
    public bool TryRandomAvailable(out Kind kind)
    {
        int count=0;kind=Kind.Power;
        for(int i=0;i<levels.Length;i++)if(CanCollect((Kind)i) && Random.Range(0,++count)==0)kind=(Kind)i;
        return count>0;
    }
    public string ChoiceDescription(Kind kind)
    {
        var attack=GetComponent<PlayerAutoAttack>();
        string title=TitleFor(kind),description="",value="";
        switch(kind)
        {
            case Kind.Power:description="모든 무기 공격력 증가";value=$"{DamageMultiplier*100:0}% → {(DamageMultiplier+.1f)*100:0}%";break;
            case Kind.Speed:description="이동 속도 증가";value=$"{MoveMultiplier*100:0}% → {(MoveMultiplier+.05f)*100:0}%";break;
            case Kind.Vitality:
                description="최대 체력 +15\n체력 15 즉시 회복";float hp=GetComponent<PlayerHealth>().maxHealth;value=$"{hp:0} → {hp+15:0}";break;
            case Kind.QuickHands:description="모든 무기 공격 간격 감소\n레이저 충전도 빨라짐";value=$"기본 간격의 {CooldownMultiplier*100:0.#}%\n→ {CooldownMultiplier*.85f*100:0.#}%";break;
            case Kind.LongReach:description="근접 공격 기본 사거리 증가";value=$"{attack.manualAttackRange:0.#}m → {Mathf.Min(6,attack.manualAttackRange+.4f):0.#}m";break;
            case Kind.WideSwing:description="근접 공격 기본 각도 증가";value=$"{attack.attackAngle:0}° → {Mathf.Min(300,attack.attackAngle+25):0}°";break;
            case Kind.BloodEdge:description="근접 공격 적중 시 체력 회복\n초당 회복량 제한 적용";value=$"{attack.lifestealPerHit:0} → {Mathf.Min(9,attack.lifestealPerHit+3):0}";break;
            case Kind.SecondWind:description="대시 재사용 대기시간 감소";float cd=GetComponent<PlayerDash>().cooldown;value=$"{cd:0.#}초 → {Mathf.Max(.5f,cd-.4f):0.#}초";break;
        }
        return $"{title}\n\n{description}\n\n{value}\n\n강화 {Level(kind)} → {Level(kind)+1} / {Limit(kind)}";
    }
    public static string TitleFor(Kind kind)
    {
        switch(kind)
        {
            case Kind.Power:return "공격력 증가";
            case Kind.Speed:return "이동 속도 증가";
            case Kind.Vitality:return "최대 체력 증가";
            case Kind.QuickHands:return "공격 속도 증가";
            case Kind.LongReach:return "근접 사거리 증가";
            case Kind.WideSwing:return "근접 공격 범위 증가";
            case Kind.BloodEdge:return "근접 적중 시 회복";
            case Kind.SecondWind:return "대시 쿨타임 감소";
            default:return "증강";
        }
    }
    public string OwnedDescription(Kind kind)
    {
        string effect="";
        var attack=GetComponent<PlayerAutoAttack>();
        switch(kind)
        {
            case Kind.Power:effect=$"모든 무기 공격력 +{(DamageMultiplier-1)*100:0}%";break;
            case Kind.Speed:effect=$"이동 속도 +{(MoveMultiplier-1)*100:0}%";break;
            case Kind.Vitality:effect=$"최대 체력 {GetComponent<PlayerHealth>().maxHealth:0} (증강 +{Level(kind)*15})";break;
            case Kind.QuickHands:effect=$"공격 간격 · 레이저 충전 {CooldownMultiplier*100:0.#}%\n기본 대비 {(1-CooldownMultiplier)*100:0.#}% 감소";break;
            case Kind.LongReach:effect=$"근접 기본 사거리 {attack.manualAttackRange:0.#}m";break;
            case Kind.WideSwing:effect=$"근접 기본 공격 각도 {attack.attackAngle:0}°";break;
            case Kind.BloodEdge:effect=$"근접 적중 시 체력 {attack.lifestealPerHit:0} 회복\n초당 최대 {attack.lifestealPerSecondCap:0.#} 회복";break;
            case Kind.SecondWind:effect=$"대시 재사용 대기시간 {GetComponent<PlayerDash>().cooldown:0.#}초";break;
        }
        return $"{TitleFor(kind)}   {Level(kind)}단계{(!CanCollect(kind)?" · 최대 강화":"")}\n{effect}";
    }
    public static string Name(Kind kind)
    {
        switch(kind)
        {
            case Kind.Power:return "POWER +10%";
            case Kind.Speed:return "SPEED +5%";
            case Kind.Vitality:return "MAX HP +15";
            case Kind.QuickHands:return "ATTACK COOLDOWN -15%";
            case Kind.LongReach:return "MELEE REACH +0.4m";
            case Kind.WideSwing:return "MELEE ARC +25";
            case Kind.BloodEdge:return "MELEE HEAL +3";
            case Kind.SecondWind:return "DASH COOLDOWN -0.4s";
            default:return "AUGMENT";
        }
    }
    public static Color ColorFor(Kind kind)
    {
        switch(kind)
        {
            case Kind.Power:return new Color(1,.45f,.15f);
            case Kind.Speed:return new Color(.2f,.8f,1);
            case Kind.QuickHands:return new Color(1,.85f,.2f);
            case Kind.LongReach:return new Color(.65f,.4f,1);
            case Kind.WideSwing:return new Color(.3f,1,.8f);
            case Kind.BloodEdge:return new Color(1,.2f,.35f);
            case Kind.SecondWind:return new Color(.7f,.85f,1);
            default:return new Color(.3f,1,.4f);
        }
    }
}
