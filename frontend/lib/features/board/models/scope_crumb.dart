/// One entry in the board's navigation trail (Space, then drill-down path).
///
/// - [id] and [spaceId] both null: the open View's root, showing each of its
///   Spaces' top level. Only a View with more than one Space has one.
/// - [id] null, [spaceId] set: that Space's top level.
/// - [id] set: that work item's children, in [spaceId].
class ScopeCrumb {
  const ScopeCrumb({required this.id, required this.title, this.spaceId});

  final String? id;
  final String title;
  final String? spaceId;
}
