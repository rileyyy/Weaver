/// A link as seen from the work item currently being viewed — [linkedWorkItemId]/
/// [linkedWorkItemTitle] are always "the other side" of the relationship.
class WorkItemLink {
  const WorkItemLink({
    required this.id,
    required this.linkedWorkItemId,
    required this.linkedWorkItemTitle,
  });

  final String id;
  final String linkedWorkItemId;
  final String linkedWorkItemTitle;
}
