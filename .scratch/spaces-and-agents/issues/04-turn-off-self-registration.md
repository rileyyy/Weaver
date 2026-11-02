# 04: Turn off self-registration

**What to build:** Nobody can create their own account any more; admins create every account from the Admin page. The login screen no longer offers registration. See ADR-0004.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The register endpoint and the register UI are removed, not just hidden
- [x] A fresh deployment still has a way to get its first admin (keep or document the existing bootstrap path)
- [x] Integration tests show registration is gone, and tests that registered users now create them as an admin. The test changes track an intentional behaviour change
- [x] The app's periodic user-list refresh exists because people could self-register; keep it only if something else still needs it
- [x] Docs no longer describe open registration
