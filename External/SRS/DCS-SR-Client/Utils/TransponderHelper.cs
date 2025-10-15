﻿using System;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;

public static class TransponderHelper
{
    public static Transponder GetTransponder(bool onlyIfOverlayControls = false)
    {
        var dcsPlayerRadioInfo = ClientStateSingleton.Instance.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent() && dcsPlayerRadioInfo.iff != null &&
            dcsPlayerRadioInfo.iff.control != Transponder.IFFControlMode.DISABLED)
        {
            if (onlyIfOverlayControls)
            {
                if (dcsPlayerRadioInfo.iff.control == Transponder.IFFControlMode.OVERLAY) return dcsPlayerRadioInfo.iff;
            }
            else
            {
                return dcsPlayerRadioInfo.iff;
            }
        }

        return null;
    }

    public static bool ToggleIdent()
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null && trans.status != Transponder.IFFStatus.OFF)
        {
            if (trans.status == Transponder.IFFStatus.NORMAL)
            {
                trans.status = Transponder.IFFStatus.IDENT;
                return true;
            }

            trans.status = Transponder.IFFStatus.NORMAL;
            return true;
        }

        return false;
    }

    public static bool Mode4Toggle()
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null) trans.mode4 = !trans.mode4;

        return false;
    }

    public static bool SetMode3(int mode3)
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null)
        {
            if (mode3 < 0)
            {
                trans.mode3 = -1;
            }
            else
            {
                var numberStr = Math.Abs(mode3).ToString().ToCharArray();

                for (var i = 0; i < numberStr.Length; i++)
                    if (int.Parse(numberStr[i].ToString()) > 7)
                        numberStr[i] = '7';

                trans.mode3 = int.Parse(new string(numberStr));
            }

            return true;
        }

        return false;
    }

    public static bool SetMode2(int mode2)
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null)
        {
            if (mode2 < 0)
            {
                trans.mode2 = -1;
            }
            else
            {
                var numberStr = Math.Abs(mode2).ToString().ToCharArray();

                for (var i = 0; i < numberStr.Length; i++)
                    if (int.Parse(numberStr[i].ToString()) > 7)
                        numberStr[i] = '7';

                trans.mode2 = int.Parse(new string(numberStr));
            }

            return true;
        }

        return false;
    }

    public static bool SetMode1(int mode1)
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null)
        {
            if (mode1 < 0)
            {
                trans.mode1 = -1;
            }
            else
            {
                //first digit 0-7 inc
                //second 0-3 inc

                var first = mode1 / 10;

                if (first > 7) first = 7;

                if (first < 0) first = 0;

                var second = mode1 % 10;

                if (second > 3) second = 3;

                trans.mode1 = first * 10 + second;
            }

            return true;
        }

        return false;
    }

    public static bool TogglePower()
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null)
        {
            if (trans.status == Transponder.IFFStatus.OFF)
                trans.status = Transponder.IFFStatus.NORMAL;
            else
                trans.status = Transponder.IFFStatus.OFF;

            return true;
        }

        return false;
    }

    public static bool SetPower(bool on)
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null)
        {
            if (on)
                trans.status = Transponder.IFFStatus.NORMAL;
            else
                trans.status = Transponder.IFFStatus.OFF;

            return true;
        }

        return false;
    }

    public static bool SetMode4(bool on)
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null)
        {
            trans.mode4 = on;
            return true;
        }

        return false;
    }

    public static bool SetIdent(bool on)
    {
        ClientStateSingleton.Instance.LastSent = 0;
        var trans = GetTransponder(true);

        if (trans != null && trans.status != Transponder.IFFStatus.OFF)
        {
            if (on)
                trans.status = Transponder.IFFStatus.IDENT;
            else
                trans.status = Transponder.IFFStatus.NORMAL;

            return true;
        }

        return false;
    }
}