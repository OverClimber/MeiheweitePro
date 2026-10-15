-- Same fixture for native validation and the PC/Android puzzle UI.
Debug.ReloadFieldBegin(0)
Debug.SetPlayerInfo(0,8000,0,0)
Debug.SetPlayerInfo(1,8000,0,0)
local host=Debug.AddCard(47095439,0,0,LOCATION_MZONE,2,POS_FACEUP_ATTACK)
local normal=Debug.AddCard(89631139,0,0,LOCATION_SZONE,0,POS_FACEUP_ATTACK)
local spell=Debug.AddCard(40619825,0,0,LOCATION_SZONE,4,POS_FACEUP_ATTACK)
Debug.AddCard(89631139,1,1,LOCATION_MZONE,2,POS_FACEUP_ATTACK)
for player=0,1 do
    for seq=0,9 do Debug.AddCard(89631139,player,player,LOCATION_DECK,seq,POS_FACEDOWN_DEFENSE) end
end
for _,c in ipairs({normal,spell}) do
    local limit=Effect.CreateEffect(c)
    limit:SetType(EFFECT_TYPE_SINGLE)
    limit:SetCode(EFFECT_EQUIP_LIMIT)
    limit:SetProperty(EFFECT_FLAG_CANNOT_DISABLE)
    limit:SetValue(function(e,target) return target==host end)
    c:RegisterEffect(limit)
end
local operation=Effect.CreateEffect(host)
operation:SetType(EFFECT_TYPE_FIELD+EFFECT_TYPE_CONTINUOUS)
operation:SetCode(EVENT_ADJUST)
operation:SetOperation(function(e)
    e:Reset()
    assert(Duel.Equip(0,normal,host),'normal equip failed')
    assert(Duel.Equip(0,spell,host),'spell equip failed')
    assert(normal:GetEquipTarget()==host and spell:GetEquipTarget()==host,'equip relations missing')
    assert(c47095439.cfilter(normal),'equipped normal cost unavailable')
    assert(not c47095439.cfilter(spell),'ordinary equip spell incorrectly accepted as cost')
    Debug.Message('EQUIP_PROBE DONE')
end)
Duel.RegisterEffect(operation,0)
Debug.ReloadFieldEnd()
