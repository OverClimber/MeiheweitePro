-- Run in actual duel processing: Debug.PreEquip alone does not change GetType.
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local hosts={}
local registered={}
local register=Card.RegisterEffect
Card.RegisterEffect=function(c,e,...)
    local result=register(c,e,...)
    registered[c]=registered[c] or {}
    table.insert(registered[c],e)
    return result
end
for i,code in ipairs({47095439,73490148,10884847,84512726}) do
    hosts[i]=Debug.AddCard(code,0,0,LOCATION_MZONE,i-1,POS_FACEUP_ATTACK)
end
local normal=Debug.AddCard(89631139,0,0,LOCATION_SZONE,0,POS_FACEUP_ATTACK)
local effect=Debug.AddCard(10007434,0,0,LOCATION_SZONE,1,POS_FACEUP_ATTACK)
local spell=Debug.AddCard(40619825,0,0,LOCATION_SZONE,2,POS_FACEUP_ATTACK)
Debug.AddCard(89631139,1,1,LOCATION_MZONE,0,POS_FACEUP_ATTACK)
for player=0,1 do
    for seq=0,9 do Debug.AddCard(89631139,player,player,LOCATION_DECK,seq,POS_FACEDOWN_DEFENSE) end
end
Card.RegisterEffect=register
local checks=0
local function check(value,message)
    assert(value,message)
    checks=checks+1
    Debug.Message('EQUIP_PROBE PASS '..message)
end
local operation=Effect.CreateEffect(hosts[1])
operation:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
operation:SetCode(EVENT_ADJUST)
operation:SetOperation(function(e)
    e:Reset()
    for _,c in ipairs({normal,effect,spell}) do
        check(Duel.Equip(0,c,hosts[1]),'actual equip '..c:GetCode())
    end
    check(not normal:IsType(TYPE_MONSTER),'current type remains equip spell')
    check(normal:IsCardType(TYPE_MONSTER),'card-type any-bit API recognizes equipped monster')
    check(not spell:IsCardType(TYPE_MONSTER),'ordinary spell never becomes monster card')
    check(normal:IsAllCardTypes(TYPE_NORMAL+TYPE_MONSTER),'card-type all-bits recognizes normal')
    for _,c in ipairs({effect,spell}) do
        check(not c:IsAllCardTypes(TYPE_NORMAL+TYPE_MONSTER),'reject non-normal '..c:GetCode())
        check(not c47095439.cfilter(c),'Sphinx rejects non-normal '..c:GetCode())
        check(not c73490148.cfilter(c),'Adjat rejects non-normal '..c:GetCode())
    end
    check(c47095439.cfilter(normal),'Sphinx accepts equipped normal cost')
    check(c73490148.cfilter(normal),'Adjat accepts equipped normal cost')
    check(c84512726.chkfilter(normal),'Nefertari counts equipped normal')
    local nekhbet
    for i=1,3 do
        for _,ability in ipairs(registered[hosts[i]]) do
            if ability:IsHasType(EFFECT_TYPE_QUICK_O) then
                check(ability:IsActivatable(0,false,false),'native activation '..hosts[i]:GetCode())
                if i==3 then nekhbet=ability end
            end
        end
    end
    check(nekhbet~=nil,'Nekhbet effect registered')
    check(c10884847.spfilter(normal,nekhbet,0),'Nekhbet accepts equipped normal summon')
    check(not c10884847.spfilter(effect,nekhbet,0),'Nekhbet rejects equipped effect monster')
    local prohibit=Effect.CreateEffect(hosts[1])
    prohibit:SetType(EFFECT_TYPE_SINGLE)
    prohibit:SetCode(EFFECT_CANNOT_TO_GRAVE_AS_COST)
    prohibit:SetValue(1)
    normal:RegisterEffect(prohibit)
    check(not c47095439.cfilter(normal),'Sphinx respects cannot pay grave cost')
    check(not c73490148.cfilter(normal),'Adjat respects cannot pay grave cost')
    prohibit:Reset()
    -- The real core transitions the equipped monster back into a monster.
    check(Duel.SpecialSummon(normal,0,0,0,false,false,POS_FACEUP_ATTACK)==1,'actual summon from equipment zone')
    check(normal:IsLocation(LOCATION_MZONE) and normal:IsType(TYPE_MONSTER),'summoned monster type restored')
    check(normal:GetEquipTarget()==nil,'old equip relation cleared on summon')
    check(Duel.Equip(0,hosts[3],normal),'Nekhbet can equip itself to summoned monster')
    check(hosts[3]:GetEquipTarget()==normal,'new equip relation established')
    Debug.Message('EQUIP_PROBE assertions='..checks)
    Debug.Message('EQUIP_PROBE DONE')
end)
Duel.RegisterEffect(operation,0)
Debug.ReloadFieldEnd()
