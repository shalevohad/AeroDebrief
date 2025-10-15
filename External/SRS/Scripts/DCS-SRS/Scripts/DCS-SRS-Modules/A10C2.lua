local _a10c2 = {}
_a10c2.enc = false
_a10c2.encKey = 1
_a10c2.volume = 1
_a10c2.lastVolPos = 0
_a10c2.increaseVol = false
_a10c2.decreaseVol = false
_a10c2.enableVolumeControl = false

function exportRadioA10C2(_data, SR)

    _data.capabilities = { dcsPtt = true, dcsIFF = true, dcsRadioSwitch = true, intercomHotMic = false, desc = "Using cockpit PTT (HOTAS Mic Switch) requires use of VoIP bindings." }

    -- Check if player is in a new aircraft
    if SR.LastKnownUnitId ~= _data.unitId then
        -- New aircraft; Reset volumes to 100%
        local _device = GetDevice(0)

        if _device then
         --   _device:set_argument_value(133, 1.0) -- VHF AM
            _device:set_argument_value(171, 1.0) -- UHF
            _device:set_argument_value(147, 1.0) -- VHF FM
            _a10c2.enc = false
            _a10c2.encKey = 1
            _a10c2.volume = 1
            _a10c2.increaseVol = false
            _a10c2.decreaseVol = false
            _a10c2.enableVolumeControl = false
        end
    end

    -- VHF AM
    -- Set radio data
    _data.radios[2].name = "AN/ARC-210 VHF/UHF"
    _data.radios[2].freq = SR.getRadioFrequency(55)
    _data.radios[2].modulation = SR.getRadioModulation(55)
    _data.radios[2].encMode = 2 -- Mode 2 is set by aircraft
    _data.radios[2].model = SR.RadioModels.AN_ARC210

    --18 : {"PREV":"PREV","comsec_mode":"KY-58 VOICE","comsec_submode":"CT","dot_mark":".","freq_label_khz":"000","freq_label_mhz":"124","ky_submode_label":"1","lower_left_corner_arc210":"","modulation_label":"AM","prev_manual_freq":"---.---","txt_RT":"RT1"}
    -- 18 : {"PREV":"PREV","comsec_mode":"KY-58 VOICE","comsec_submode":"CT-TD","dot_mark":".","freq_label_khz":"000","freq_label_mhz":"124","ky_submode_label":"4","lower_left_corner_arc210":"","modulation_label":"AM","prev_manual_freq":"---.---","txt_RT":"RT1"}
    
    pcall(function() 
        local _radioDisplay = SR.getListIndicatorValue(18)

        if _radioDisplay["COMSEC"] == "COMSEC" then
            _a10c2.enableVolumeControl = true
        else
            _a10c2.enableVolumeControl = false
        end

        if _radioDisplay.comsec_submode and _radioDisplay.comsec_submode == "PT" then
            
            _a10c2.encKey = tonumber(_radioDisplay.ky_submode_label)
            _a10c2.enc = false

        elseif _radioDisplay.comsec_submode and (_radioDisplay.comsec_submode == "CT-TD" or _radioDisplay.comsec_submode == "CT") then

            _a10c2.encKey = tonumber(_radioDisplay.ky_submode_label)
            _a10c2.enc = true
         
        end
    end)

    local _current = SR.getButtonPosition(552)
    local _delta = _a10c2.lastVolPos - _current
    _a10c2.lastVolPos = _current

    if _delta > 0 then
        _a10c2.decreaseVol = true

    elseif _delta < 0 then
        _a10c2.increaseVol = true
    else
        _a10c2.increaseVol = false
        _a10c2.decreaseVol = false
    end
       
    if _a10c2.enableVolumeControl then
        if _a10c2.increaseVol then
            _a10c2.volume = _a10c2.volume + 0.05
        elseif _a10c2.decreaseVol then
            _a10c2.volume = _a10c2.volume - 0.05
        end

        if _a10c2.volume > 1.0 then
            _a10c2.volume = 1.0
        end

        if _a10c2.volume < 0.0 then
            _a10c2.volume = 0
        end
    end 

    _data.radios[2].volume = _a10c2.volume * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 225, { 0.0, 1.0 }, false) * SR.getButtonPosition(226)
    _data.radios[2].encKey = _a10c2.encKey
    _data.radios[2].enc = _a10c2.enc

    -- CREDIT: Recoil - thank you!
    -- Check ARC-210 function mode (0 = OFF, 1 = TR+G, 2 = TR, 3 = ADF, 4 = CHG PRST, 5 = TEST, 6 = ZERO)
    local arc210ModeKnob = SR.getSelectorPosition(551, 0.1)
    if arc210ModeKnob == 1 and _data.radios[2].freq > 1000 then
        -- Function dial set to TR+G
        -- Listen to Guard as well as designated frequency
        if (_data.radios[2].freq >= (108.0 * 1000000)) and (_data.radios[2].freq < (156.0 * 1000000)) then
            -- Frequency between 108.0 and 156.0 MHz, using VHF Guard
            _data.radios[2].secFreq = 121.5 * 1000000
        else
            -- Other frequency, using UHF Guard
            _data.radios[2].secFreq = 243.0 * 1000000
        end
    else
        -- Function dial set to OFF, TR, ADF, CHG PRST, TEST or ZERO
        -- Not listening to Guard secondarily
        _data.radios[2].secFreq = 0
    end

    -- UHF
    -- Set radio data
    _data.radios[3].name = "AN/ARC-164 UHF"
    _data.radios[3].freq = SR.getRadioFrequency(54)
    _data.radios[3].model = SR.RadioModels.AN_ARC164
    
    local modulation = SR.getSelectorPosition(162, 0.1)

    --is HQ selected (A on the Radio)
    if modulation == 2 then
        _data.radios[3].modulation = 4
    else
        _data.radios[3].modulation = 0
    end

    _data.radios[3].volume = SR.getRadioVolume(0, 171, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 227, { 0.0, 1.0 }, false) * SR.getButtonPosition(228)
    _data.radios[3].encMode = 2 -- Mode 2 is set by aircraft

    -- Check UHF frequency mode (0 = MNL, 1 = PRESET, 2 = GRD)
    local _selector = SR.getSelectorPosition(167, 0.1)
    if _selector == 1 then
        -- Using UHF preset channels
        local _channel = SR.getSelectorPosition(161, 0.05) + 1 --add 1 as channel 0 is channel 1
        _data.radios[3].channel = _channel
    end

    -- Check UHF function mode (0 = OFF, 1 = MAIN, 2 = BOTH, 3 = ADF)
    local uhfModeKnob = SR.getSelectorPosition(168, 0.1)
    if uhfModeKnob == 2 and _data.radios[3].freq > 1000 then
        -- Function dial set to BOTH
        -- Listen to Guard as well as designated frequency
        _data.radios[3].secFreq = 243.0 * 1000000
    else
        -- Function dial set to OFF, MAIN, or ADF
        -- Not listening to Guard secondarily
        _data.radios[3].secFreq = 0
    end

    -- VHF FM
    -- Set radio data
    _data.radios[4].name = "AN/ARC-186(V)FM"
    _data.radios[4].freq = SR.getRadioFrequency(56)
    _data.radios[4].modulation = 1
    _data.radios[4].volume = SR.getRadioVolume(0, 147, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 238, { 0.0, 1.0 }, false) * SR.getRadioVolume(0, 223, { 0.0, 1.0 }, false) * SR.getButtonPosition(224)
    _data.radios[4].encMode = 2 -- mode 2 enc is set by aircraft & turned on by aircraft
    _data.radios[4].model = SR.RadioModels.AN_ARC186

    -- KY-58 Radio Encryption
    -- Check if encryption is being used
    local _ky58Power = SR.getButtonPosition(784)
    if _ky58Power > 0.5 and SR.getButtonPosition(783) == 0 then
        -- mode switch set to OP and powered on
        -- Power on!

        local _radio = nil
        if SR.round(SR.getButtonPosition(781), 0.1) == 0.2 and SR.getSelectorPosition(149, 0.1) >= 2 then -- encryption disabled when EMER AM/FM selected
            --crad/2 vhf - FM
            _radio = _data.radios[4]
        elseif SR.getButtonPosition(781) == 0 and _selector ~= 2 then -- encryption disabled when GRD selected
            --crad/1 uhf
            _radio = _data.radios[3]
        end

        -- Get encryption key
        local _channel = SR.getSelectorPosition(782, 0.1) + 1

        if _radio ~= nil and _channel ~= nil then
            -- Set encryption key for selected radio
            _radio.encKey = _channel
            _radio.enc = true
        end
    end

 -- Mic Switch Radio Select and Transmit - by Dyram
    -- Check Mic Switch position (UP: 751 1.0, DOWN: 751 -1.0, FWD: 752 1.0, AFT: 752 -1.0)
    -- ED broke this as part of the VoIP work
    if SR.getButtonPosition(752) == 1 then
        -- Mic Switch FWD pressed
        -- Check Intercom panel Rotary Selector Dial (0: INT, 1: FM, 2: VHF, 3: HF, 4: "")
        if SR.getSelectorPosition(239, 0.1) == 2 then
            -- Intercom panel set to VHF
            _data.selected = 1 -- radios[2] VHF AM
            _data.ptt = true
        elseif SR.getSelectorPosition(239, 0.1) == 0 then
            -- Intercom panel set to INT
            -- Intercom not functional, but select it anyway to be proper
            _data.selected = 0 -- radios[1] Intercom
        else
            _data.selected = -1
        end
    elseif SR.getButtonPosition(751) == -1 then
        -- Mic Switch DOWN pressed
        _data.selected = 2 -- radios[3] UHF
        _data.ptt = true
    elseif SR.getButtonPosition(752) == -1 then
        -- Mic Switch AFT pressed
        _data.selected = 3 -- radios[4] VHF FM
        _data.ptt = true
    else
        -- Mic Switch released
        _data.selected = -1
        _data.ptt = false
    end

    _data.control = 1 -- Overlay  

    -- Handle transponder

    _data.iff = {status=0,mode1=0,mode2=-1,mode3=0,mode4=false,control=0,expansion=false}

    local iffPower =  SR.getSelectorPosition(200,0.1)

    local iffIdent =  SR.getButtonPosition(207) -- -1 is off 0 or more is on

    if iffPower >= 2 then
        _data.iff.status = 1 -- NORMAL

        if iffIdent == 1 then
            _data.iff.status = 2 -- IDENT (BLINKY THING)
        end

        -- SR.log("IFF iffIdent"..iffIdent.."\n\n")
        -- MIC mode switch - if you transmit on UHF then also IDENT
        -- https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/408
        if iffIdent == -1 then

            _data.iff.mic = 2

            if _data.ptt and _data.selected == 2 then
                _data.iff.status = 2 -- IDENT (BLINKY THING)
            end
        end
    end

    local mode1On =  SR.getButtonPosition(202)

    _data.iff.mode1 = SR.round(SR.getButtonPosition(209), 0.1)*100+SR.round(SR.getButtonPosition(210), 0.1)*10

    if mode1On ~= 0 then
        _data.iff.mode1 = -1
    end

    local mode3On =  SR.getButtonPosition(204)

    _data.iff.mode3 = SR.round(SR.getButtonPosition(211), 0.1) * 10000 + SR.round(SR.getButtonPosition(212), 0.1) * 1000 + SR.round(SR.getButtonPosition(213), 0.1)* 100 + SR.round(SR.getButtonPosition(214), 0.1) * 10

    if mode3On ~= 0 then
        _data.iff.mode3 = -1
    elseif iffPower == 4 then
        -- EMERG SETTING 7770
        _data.iff.mode3 = 7700
    end

    local mode4On =  SR.getButtonPosition(208)

    if mode4On ~= 0 then
        _data.iff.mode4 = true
    else
        _data.iff.mode4 = false
    end

    if SR.getAmbientVolumeEngine()  > 10 then
        -- engine on

        local _door = SR.getButtonPosition(7)

        if _door > 0.1 then 
            _data.ambient = {vol = 0.3,  abType = 'a10' }
        else
            _data.ambient = {vol = 0.2,  abType = 'a10' }
        end 
    
    else
        -- engine off
        _data.ambient = {vol = 0, abType = 'a10' }
    end

    -- SR.log("IFF STATUS"..SR.JSON:encode(_data.iff).."\n\n")
    return _data
end

local result = {
    register = function(SR)
        SR.exporters["A-10C_2"] = exportRadioA10C2
    end,
}
return result
