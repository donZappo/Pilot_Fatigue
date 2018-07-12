# Pilot_Fatigue
Pilot Fatigue Mod for BATTLETECH
Pilot Fatigue:

***SUMMARY***

Now after every mission your pilots that participated are now fatigued. They will have decreased Gunnery, Piloting, and Tactics while fatigued. If they drop while 
fatigued, they have a chance to suffer a "Light Injury." High Guts helps to lower their fatigue and resist light injuries. Morale also helps them to lower their
fatigue. Treat your MechWarriors well and they'll keep fighting until the bitter end!



***CALCULATIONS***

They are out for the following time (default values used): 

	-Time Out = 7 - Guts/2 - Morale threshold
	-Morale threshold: +-1 for +-5 morale from Starting Morale Value, +-2 for +-15 from start
	-1 point piloting, tactics, and gunnery lost for every 2.5 days of fatigue, rounded up.
	-Chance to resist a light injury = guts * 10%



***ADJUSTABLE VALUES***

Values that can be changed in the settings.json:

	"FatigueTimeStart" : 7, 	(How many days to start the fatigue calculation at)
	"StartingMorale" : 25, 		(enter starting morale here for MoraleTier determinations)
	"FatigueMinimum" : 0, 		(minimum days that a pilot will be fatigued for after a mission)
	"MoralePositiveTierOne" : 5, 	(Positive difference from StartingMorale to reduce fatigue by 1 day)
	"MoralePostiveTierTwo" : 15, 	(Positive difference from StartingMorale to reduce fatigue by 2 day)
	"MoraleNegativeTierOne" : -5, 	(Negative difference from StartingMorale to increase fatigue by 1 day)
	"MoraleNegativeTierTwo" : -15, 	(Negative difference from StartingMorale to increase fatigue by 2 day)
	"FatigueFactor" : 3,		(For skill degradation with fatigue, how many days is used in the calculation)
	"InjuriesHurt" : true, 		(makes your skills degrade one point per pilot injury)



***INSTALLATION NOTES***
1) This Mod Conflicts with No Time To Bleed and InjuriesHurt. It will not run if you have these installed and enabled! 

2) Unzip the folder and add it to your BATTLETECH\mods folder. 

3) Make sure you hae BMTL and ModTek installed.
