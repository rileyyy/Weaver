# 40: Mobile safe areas, keyboard insets and back button

**What to build:** On phones, nothing sits under the status bar or gesture area, the keyboard never covers the field being typed in (including in dialogs), and the system back button closes the top dialog or steps out of drill-down before leaving the app.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Every screen respects safe areas
- [ ] Dialogs and forms scroll the focused field above the keyboard
- [ ] Back closes the top dialog or sheet, then steps up one drill-down level, then exits
- [ ] Verified by hand on an Android device or emulator; widget tests where practical
