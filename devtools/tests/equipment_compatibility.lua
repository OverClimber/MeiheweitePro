local reload=assert(load([====[
@COMPATIBILITY@
]====]))
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local c=Debug.AddCard(89631139,0,0,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
Debug.ReloadFieldEnd()
local all,any,get=Card.IsAllCardTypes,Card.IsCardType,Card.GetCardType
local custom=function() return 'custom' end
Card.IsAllCardTypes=custom
Card.IsCardType=custom
reload()
assert(Card.IsAllCardTypes==custom and Card.IsCardType==custom,'custom/native methods overwritten')
Card.IsAllCardTypes=all
Card.IsCardType=any
Card.GetCardType=function() return TYPE_SPELL+TYPE_EQUIP end
assert(not c:IsCardType(TYPE_MONSTER),'native card-type provider ignored')
assert(c:IsAllCardTypes(TYPE_SPELL+TYPE_EQUIP),'native all-bits provider ignored')
Card.GetCardType=get
-- Old cores accept effect codes; emulate newer constants without changing production globals.
EFFECT_ADD_CARD_TYPE=10001001
EFFECT_REMOVE_CARD_TYPE=10001002
EFFECT_CHANGE_CARD_TYPE=10001003
local function change(code,value)
    local e=Effect.CreateEffect(c)
    e:SetType(EFFECT_TYPE_SINGLE)
    e:SetCode(code)
    e:SetValue(value)
    c:RegisterEffect(e)
end
change(EFFECT_ADD_CARD_TYPE,TYPE_EFFECT)
assert(c:IsAllCardTypes(TYPE_MONSTER+TYPE_NORMAL+TYPE_EFFECT),'add card type ignored')
change(EFFECT_REMOVE_CARD_TYPE,TYPE_NORMAL)
assert(not c:IsCardType(TYPE_NORMAL),'remove card type ignored')
change(EFFECT_CHANGE_CARD_TYPE,TYPE_SPELL)
assert(c:IsAllCardTypes(TYPE_SPELL) and not c:IsCardType(TYPE_MONSTER),'change card type or ordering ignored')
change(EFFECT_ADD_CARD_TYPE,function(e,card)
    assert(card==c,'value callback has wrong card argument')
    assert(card:IsCardType(TYPE_SPELL),'recursive card-type query lost previous value')
    return TYPE_EQUIP
end)
assert(c:IsAllCardTypes(TYPE_SPELL+TYPE_EQUIP),'function-valued effect ignored')
Debug.Message('EQUIP_PROBE compatibility preservation, native provider, ordered effects and recursion passed')
