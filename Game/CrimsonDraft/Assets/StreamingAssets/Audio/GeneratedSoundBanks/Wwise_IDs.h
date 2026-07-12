/////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Audiokinetic Wwise generated include file. Do not edit.
//
/////////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef __WWISE_IDS_H__
#define __WWISE_IDS_H__

#include <AK/SoundEngine/Common/AkTypes.h>

namespace AK
{
    namespace EVENTS
    {
        static const AkUniqueID PLAY_AMBIENCESC = 433061256U;
        static const AkUniqueID PLAY_DOORLOCKEDINTERACT = 1907839430U;
        static const AkUniqueID PLAY_DOORSSC = 5477307U;
        static const AkUniqueID PLAY_FIREGUNSSC = 2180097029U;
        static const AkUniqueID PLAY_FOOTSTEPSSC = 3119045201U;
        static const AkUniqueID PLAY_HEARTBEATSFX = 2420947455U;
        static const AkUniqueID PLAY_MSC_MANAGER = 3143732789U;
        static const AkUniqueID PLAY_RADIOSC = 3489459273U;
        static const AkUniqueID PLAY_SHELLCASING = 1647385053U;
        static const AkUniqueID PLAY_WEATHERBC = 384760087U;
        static const AkUniqueID PLAY_ZOMBIE_DAMAGE = 876165436U;
        static const AkUniqueID PLAY_ZOMBIESC = 2417702238U;
    } // namespace EVENTS

    namespace STATES
    {
        namespace AMBIENTS
        {
            static const AkUniqueID GROUP = 4095160042U;

            namespace STATE
            {
                static const AkUniqueID BALCONY = 827522479U;
                static const AkUniqueID BATHROOM = 1831461191U;
                static const AkUniqueID HALLWAY = 2198133169U;
                static const AkUniqueID KITCHEN = 1586158131U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID ROOM = 2077253480U;
                static const AkUniqueID STAIRS = 1289942167U;
            } // namespace STATE
        } // namespace AMBIENTS

        namespace PLAYERSTATE
        {
            static const AkUniqueID GROUP = 3285234865U;

            namespace STATE
            {
                static const AkUniqueID COMBAT = 2764240573U;
                static const AkUniqueID DIALOGUE = 3930136735U;
                static const AkUniqueID GAMEOVER = 4158285989U;
                static const AkUniqueID MENU = 2607556080U;
                static const AkUniqueID NAVIGATION = 1082482811U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID SAFEROOM = 604931459U;
            } // namespace STATE
        } // namespace PLAYERSTATE

    } // namespace STATES

    namespace SWITCHES
    {
        namespace DOORTYPE
        {
            static const AkUniqueID GROUP = 1569636917U;

            namespace SWITCH
            {
                static const AkUniqueID CLOSE = 1451272583U;
                static const AkUniqueID METAL = 2473969246U;
                static const AkUniqueID OPEN = 3072142513U;
                static const AkUniqueID WOOD = 2058049674U;
            } // namespace SWITCH
        } // namespace DOORTYPE

        namespace GUNTYPE
        {
            static const AkUniqueID GROUP = 1613795745U;

            namespace SWITCH
            {
                static const AkUniqueID PISTOLS = 1901565043U;
                static const AkUniqueID REPISTOLS = 533160694U;
                static const AkUniqueID RESHOTGUN = 192730228U;
                static const AkUniqueID SHOTGUN = 51683977U;
            } // namespace SWITCH
        } // namespace GUNTYPE

        namespace MARINERASECTOR
        {
            static const AkUniqueID GROUP = 994385550U;

            namespace SWITCH
            {
                static const AkUniqueID DECKB = 1199012608U;
                static const AkUniqueID DECKC = 1199012609U;
                static const AkUniqueID DOORS = 2150196036U;
                static const AkUniqueID LABORATORY = 766371778U;
            } // namespace SWITCH
        } // namespace MARINERASECTOR

        namespace RADIOTYPE
        {
            static const AkUniqueID GROUP = 1568172590U;

            namespace SWITCH
            {
                static const AkUniqueID MORSECODE = 317532818U;
                static const AkUniqueID RADIOPROXIMITY = 3137341501U;
            } // namespace SWITCH
        } // namespace RADIOTYPE

        namespace SURFACE
        {
            static const AkUniqueID GROUP = 1834394558U;

            namespace SWITCH
            {
                static const AkUniqueID CARPET = 2412606308U;
                static const AkUniqueID CONCRETE = 841620460U;
                static const AkUniqueID METAL = 2473969246U;
            } // namespace SWITCH
        } // namespace SURFACE

        namespace ZOMBIESTATE
        {
            static const AkUniqueID GROUP = 1755117202U;

            namespace SWITCH
            {
                static const AkUniqueID ATTACKING = 1641806523U;
                static const AkUniqueID DAMAGED = 3258988170U;
                static const AkUniqueID DYING = 3328495488U;
                static const AkUniqueID EATING = 2071877439U;
                static const AkUniqueID IDLE = 1874288895U;
            } // namespace SWITCH
        } // namespace ZOMBIESTATE

    } // namespace SWITCHES

    namespace GAME_PARAMETERS
    {
        static const AkUniqueID INSIDESTORMFORCE = 3108935281U;
        static const AkUniqueID MUSICHPF = 3273766828U;
        static const AkUniqueID OUTSIDESTORMFORCE = 1297380730U;
        static const AkUniqueID PLAYERLIFE = 444815956U;
        static const AkUniqueID SIGNALPROXIMITY = 3903230064U;
    } // namespace GAME_PARAMETERS

    namespace BANKS
    {
        static const AkUniqueID INIT = 1355168291U;
        static const AkUniqueID MAIN = 3161908922U;
    } // namespace BANKS

    namespace BUSSES
    {
        static const AkUniqueID ENEMIES = 2242381963U;
        static const AkUniqueID MASTER_AUDIO_BUS = 3803692087U;
        static const AkUniqueID MX = 1685527054U;
        static const AkUniqueID PLAYER = 1069431850U;
        static const AkUniqueID SFX = 393239870U;
        static const AkUniqueID UI = 1551306167U;
        static const AkUniqueID WORLD = 2609808943U;
    } // namespace BUSSES

    namespace AUX_BUSSES
    {
        static const AkUniqueID AUX_REVERB = 425396316U;
    } // namespace AUX_BUSSES

    namespace AUDIO_DEVICES
    {
        static const AkUniqueID NO_OUTPUT = 2317455096U;
        static const AkUniqueID SYSTEM = 3859886410U;
    } // namespace AUDIO_DEVICES

}// namespace AK

#endif // __WWISE_IDS_H__
