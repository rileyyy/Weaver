# 43: Delete merged branches

**What to build:** Branches already merged into `master` are deleted locally and on the remote, so the branch list shows only work in flight. `main` is kept: it's the protected historical branch.

**Blocked by:** None (can start immediately)

**Status:** done

- [ ] List branches fully merged into `master`, excluding `master` and `main`, and review the list
- [ ] Delete them on the remote and locally
- [ ] No unmerged branch is deleted
