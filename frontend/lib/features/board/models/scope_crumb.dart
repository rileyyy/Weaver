/// One entry in the board's navigation trail: the work item whose direct
/// children are shown as swimlanes. [id] is null only for the root entry:
/// the current Space's top-level items, titled with the Space's name.
class ScopeCrumb {
  const ScopeCrumb({required this.id, required this.title});

  final String? id;
  final String title;
}
