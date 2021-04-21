# Pilot_Fatigue
Pilot Fatigue Mod for BATTLETECH
Pilot Fatigue:

***SUMMARY***

Now after every mission your pilots that participated are now fatigued. They will generate less Resolve and also suffer from Low Spirits if they drop while Fatigued. In addition, if they drop while fatigued, they have a chance to suffer a "Light Injury." High Guts helps to lower their fatigue and resist light injuries. Morale also helps them to lower theirfatigue. Treat your MechWarriors well and they'll keep fighting until the bitter end!



***CALCULATIONS***

They are out for the following time (default values used): 

	-Time Out = 7 - Guts/2 - Morale threshold
	-Morale threshold: +-1 for +-5 morale from Starting Morale Value, +-2 for +-15 from start
	-1 point piloting, tactics, and gunnery lost for every 2.5 days of fatigue, rounded up.
	-Chance to resist a light injury = guts * 10%



***ADJUSTABLE VALUES***

Values that can be changed in the mod.json:

	"FatigueTimeStart" : 7, 		How many days to start the fatigue calculation at?
	"FatigueMinimum" : 0, 			Minimum days that a pilot will be fatigued for after a mission?
	"MoralePositiveTierOne" : 5, 		Positive difference from StartingMorale to reduce fatigue by 1 day.
	"MoralePostiveTierTwo" : 15, 		Positive difference from StartingMorale to reduce fatigue by 2 day.
	"MoraleNegativeTierOne" : -5, 		Negative difference from StartingMorale to increase fatigue by 1 day.
	"MoraleNegativeTierTwo" : -15, 		Negative difference from StartingMorale to increase fatigue by 2 day.
	"FatigueFactor" : 2.5,			For skill degradation with fatigue, how many days is used in the calculation?
	"InjuriesHurt" : false, 		Makes your skills degrade one point per pilot injury.
	"FatigueReducesResolve" : true,		Does Fatigue reduce Resolve?
	"FatigueReducesSkills" : false,		Does Fatigue reduce Skills?
	"FatigueResolveFactor" : 2.5,		Same as with the FatigueFactor but for Resolve.
	"FatigueCausesLowSpirites: true,	Does dropping while Fatigued cause Low Spirits?
	"LowMoraleTime" : 14,			How long does Low Spirits last if you drop while Fatigued?
	"LightInjuriesOn" : false,		Allow light injuries? 
	"MaximumFatigueTime" : 14,		Maximum fatigue days allowed per pilot.
	"AllowNegativeResolve" : false		Is resolve per turn allowed to go negative?
	"MechDamageMaxDays" : 5			Maximum Days added due to Mech Damage?
	
	"QuirksEnabled" : true,					Do Pilot Quirks effect fatigue and skill calculations? 
	"pilot_athletic_FatigueDaysReduction" : 1,		Additional days fatigue is reduced by for Athletic pilots
	"pilot_athletic_FatigueDaysReductionFactor" : 0.5,	Base reduction factor for calculating fatigue for Athletic pilots
	"pilot_wealthy_extra_fatigue" : 2,			How many extra days should a Wealthy pilot be given?



***INSTALLATION NOTES***
1) This Mod Conflicts with No Time To Bleed and InjuriesHurt. It will not run if you have these installed and enabled! 

2) Unzip the folder and add it to your BATTLETECH\mods folder. 

3) Make sure you have ModTek installed.
