# 22: Date-window rule and the Backlog

**What to build:** Board and Roadmap show only items whose dates overlap the window, so someday items stop cluttering "now". A parent with no dates still appears when any child matches; a parent with its own dates shows its own Roadmap bar. Every non-Done item with no dates lives in a new **Backlog** list, where it can be given dates. See ADR-0003.

**Blocked by:** 18

**Status:** done

- [x] Board and Roadmap apply the new rule through the server query (no client logic)
- [x] The Roadmap draws undated parents as group rows without a bar, and dated parents with their own bar
- [x] A Backlog list shows non-Done undated items for the current Space, grouped by parent
- [x] Setting dates on a Backlog item moves it out of the Backlog
- [x] Docs no longer describe a missing date as open-ended

## Comments

**2026-11-10, on implementation (`feature/date-window-rule-and-backlog`):**

- **What was already there:** the server query (ticket 17) already applied the rule and had `backlogOnly`, and the header's time-frame filter already went through it (ticket 18). So the Board needed nothing new.
- **"The window":** for the Board this is the header's time-frame filter; with none set, the swim lanes still show every item, undated ones included. For the Roadmap it's the days its timeline shows, cut to the header's range. The Roadmap is time-framed, so it never shows Backlog items, even with no header filter.
- **Roadmap rows** are the server's matches under their ancestors. An undated item only comes back as the parent of a match, so every undated row is drawn as a group row: bold title, no bar. A dated parent outside the window still shows as the context of a match, with no bar in view.
- **Backlog tab:** `backlogOnly` for the current Space, grouped by parent (top level first, then parents by title). It ignores the header's search, tags and range. After any save while the tab shows it asks again, so an item leaves when the server stops listing it. An item created elsewhere while the tab is open only appears on the next visit, because a create isn't a save.
- **Not verified here:** nobody ran the app by hand. Every criterion is covered by unit and widget tests, and the backend is unchanged.
