#Requires AutoHotkey v2.0
#SingleInstance Force

CoordMode "Mouse", "Screen"

weapons := [
    {category: "Primary", name: "Distortion"},
    {category: "Primary", name: "Permafrost"},
    {category: "Primary", name: "Energy Rifle"},
    {category: "Primary", name: "Flamethrower"},
    {category: "Primary", name: "Grenade Launcher"},
    {category: "Primary", name: "Minigun"},
    {category: "Primary", name: "Paintball Gun"},
    {category: "Primary", name: "Assault Rifle"},
    {category: "Primary", name: "Bow"},
    {category: "Primary", name: "Burst Rifle"},
    {category: "Primary", name: "Crossbow"},
    {category: "Primary", name: "Gunblade"},
    {category: "Primary", name: "RPG"},
    {category: "Primary", name: "Shotgun"},
    {category: "Primary", name: "Sniper"},
    {category: "Secondary", name: "Warper"},
    {category: "Secondary", name: "Energy Pistols"},
    {category: "Secondary", name: "Exogun"},
    {category: "Secondary", name: "Slingshot"},
    {category: "Secondary", name: "Daggers"},
    {category: "Secondary", name: "Flare Gun"},
    {category: "Secondary", name: "Handgun"},
    {category: "Secondary", name: "Revolver"},
    {category: "Secondary", name: "Shorty"},
    {category: "Secondary", name: "Spray"},
    {category: "Secondary", name: "Uzi"},
    {category: "Melee", name: "Maul"},
    {category: "Melee", name: "Spear"},
    {category: "Melee", name: "Trowel"},
    {category: "Melee", name: "Battle Axe"},
    {category: "Melee", name: "Chainsaw"},
    {category: "Melee", name: "Fists"},
    {category: "Melee", name: "Katana"},
    {category: "Melee", name: "Knife"},
    {category: "Melee", name: "Riot Shield"},
    {category: "Melee", name: "Scythe"},
    {category: "Utility", name: "Grappler"},
    {category: "Utility", name: "Medkit"},
    {category: "Utility", name: "Subspace Tripmine"},
    {category: "Utility", name: "Warpstone"},
    {category: "Utility", name: "Flashbang"},
    {category: "Utility", name: "Freeze Ray"},
    {category: "Utility", name: "Grenade"},
    {category: "Utility", name: "Jump Pad"},
    {category: "Utility", name: "Molotov"},
    {category: "Utility", name: "Satchel"},
    {category: "Utility", name: "Smoke Grenade"},
    {category: "Utility", name: "War Horn"}
]

outputDirectory := A_ScriptDir "\recorded_positions"
outputFile := outputDirectory "\list_weapon_positions.csv"
positions := []
DirCreate outputDirectory

recorder := Gui("+AlwaysOnTop", "SleepStrap List Weapon Recorder")
recorder.SetFont("s10", "Segoe UI")
recorder.MarginX := 18
recorder.MarginY := 16
recorder.AddText("w420", "Record every List weapon in order. Keep the active category open and begin at the top.")
nextText := recorder.AddText("xm y+14 w420 cFFFFFF", "Next: Primary — Distortion")
progressText := recorder.AddText("xm y+8 w420 cA0A0A0", "0 / 48 weapons")
recorder.AddText("xm y+12 w420 c808080", "Hover the weapon and press 1. The recorder scrolls after every seven weapons. Ctrl+Z undoes.")
recorder.OnEvent("Close", (*) => ExitApp())
recorder.Show()

1::RecordWeapon()
Numpad1::RecordWeapon()
^z::UndoWeapon()
F8::ShowProgress()
F10::ResetRecording()
Esc::ExitApp()

RecordWeapon() {
    global positions, weapons, nextText, progressText, outputFile
    if positions.Length >= weapons.Length
        return

    MouseGetPos &mouseX, &mouseY
    weapon := weapons[positions.Length + 1]
    categoryIndex := GetCategoryIndex(positions.Length + 1)
    positions.Push({category: weapon.category, categoryIndex: categoryIndex, name: weapon.name, x: mouseX, y: mouseY})
    SavePositions()
    SoundBeep 880, 45
    progressText.Text := positions.Length " / " weapons.Length " weapons"

    if positions.Length = weapons.Length {
        nextText.Text := "Complete — send list_weapon_positions.csv back to Codex"
        MsgBox "All 48 List weapon positions were recorded.`n`nSaved to:`n" outputFile,
            "List recording complete", "Iconi"
        return
    }

    nextWeapon := weapons[positions.Length + 1]
    nextText.Text := "Next: " nextWeapon.category " — " nextWeapon.name

    if nextWeapon.category != weapon.category {
        ToolTip "Open " nextWeapon.category " and scroll fully to the top.`nThen hover " nextWeapon.name " and press 1."
        SetTimer () => ToolTip(), -3000
        return
    }

    if Mod(categoryIndex, 7) = 0 {
        Loop 7 {
            Send "{WheelDown}"
            Sleep 18
        }
        ToolTip "Scrolled to the next seven " weapon.category " weapons.`nNext: " nextWeapon.name
        SetTimer () => ToolTip(), -2200
    }
}

GetCategoryIndex(globalIndex) {
    global weapons
    category := weapons[globalIndex].category
    index := 0
    Loop globalIndex {
        if weapons[A_Index].category = category
            index += 1
    }
    return index
}

UndoWeapon() {
    global positions, weapons, nextText, progressText
    if positions.Length = 0
        return

    removed := positions.Pop()
    SavePositions()
    next := weapons[positions.Length + 1]
    nextText.Text := "Next: " next.category " — " next.name
    progressText.Text := positions.Length " / " weapons.Length " weapons"
    ToolTip "Removed " removed.name ". Reposition the List manually if that crossed a scroll boundary."
    SetTimer () => ToolTip(), -2200
}

ResetRecording() {
    global positions, weapons, nextText, progressText
    if MsgBox("Erase this List recording and restart at Distortion?", "Restart List recording", "YesNo Icon! Default2") != "Yes"
        return
    positions := []
    SavePositions()
    nextText.Text := "Next: Primary — Distortion"
    progressText.Text := "0 / " weapons.Length " weapons"
}

ShowProgress() {
    global positions, weapons, outputFile
    MsgBox positions.Length " / " weapons.Length " weapons recorded.`n`n" outputFile,
        "List recorder progress", "Iconi"
}

SavePositions() {
    global positions, outputFile
    csv := "category,index,weapon,x,y`n"
    for position in positions
        csv .= position.category "," position.categoryIndex "," position.name "," position.x "," position.y "`n"
    if FileExist(outputFile)
        FileDelete outputFile
    FileAppend csv, outputFile, "UTF-8"
}
