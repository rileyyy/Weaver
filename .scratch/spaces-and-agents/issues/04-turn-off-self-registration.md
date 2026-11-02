# 04: Turn off self-registration

**What to build:** Nobody can create their own account any more; admins create every account from the Admin page. The login screen no longer offers registration. See ADR-0004.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The register endpoint and the register UI are removed, not just hidden
- [ ] A fresh deployment still has a way to get its first admin (keep or document the existing bootstrap path)
- [ ] Integration tests show registration is gone, and tests that registered users now create them as an admin. The test changes track an intentional behaviour change
- [ ] The app's periodic user-list refresh exists because people could self-register; keep it only if something else still needs it
- [ ] Docs no longer describe open registration
