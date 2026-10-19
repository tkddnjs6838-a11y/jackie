using UnityEngine;

// One shared texture for every illustrated augment and the matching HUD icons.
public static class GameUiArt
{
    static readonly Sprite[] icons=new Sprite[8];
    static Texture2D atlas;
    public static Sprite Icon(CharacterGrowth.Kind kind)
    {
        int index=kind==CharacterGrowth.Kind.DashStrike?1:kind==CharacterGrowth.Kind.Shatter?5:kind==CharacterGrowth.Kind.SecondWind?3:(int)kind;
        if(icons[index]!=null)return icons[index];
        if(atlas==null)atlas=Resources.Load<Texture2D>("UI/AugmentIllustrations");
        if(atlas==null)return null;
        float w=atlas.width/4f,h=atlas.height/2f;
        return icons[index]=Sprite.Create(atlas,new Rect(index%4*w,(1-index/4)*h,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
    }
    public static Color Accent(CharacterGrowth.Kind kind)
    {
        switch(kind)
        {
            case CharacterGrowth.Kind.Speed:case CharacterGrowth.Kind.LongReach:return GameUiStyle.Blue;
            case CharacterGrowth.Kind.QuickHands:case CharacterGrowth.Kind.SecondWind:return GameUiStyle.Purple;
            case CharacterGrowth.Kind.Vitality:case CharacterGrowth.Kind.BloodEdge:return GameUiStyle.Pink;
            default:return GameUiStyle.Gold;
        }
    }
}
