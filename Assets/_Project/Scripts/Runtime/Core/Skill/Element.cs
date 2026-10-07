namespace Game.Core
{
    /// <summary>스킬의 속성. 원문 용어는 비둔·화둔·수둔·풍둔·뇌둔·토둔이고 이 프로젝트에서는 무·화·빙·풍·뇌·토속성이라 부른다(docs/ninjutsu/README.md 용어 대응).
    /// 적의 약점·저항이 이 값을 보고 피해를 바꾼다. 토속성은 지상 공격이라 공중 적에게는 통하지 않는다(적의 토속성 저항 100%로 표현한다)</summary>
    public enum Element { Neutral, Fire, Ice, Wind, Lightning, Earth }
}
