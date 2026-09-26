/// One direct child of the work item being viewed, as shown in its "Sub-Items"
/// section — just enough to render a tappable row, not the full detail
/// record ([WorkItemDetail]) a child's own detail view would load.
class WorkItemChildSummary {
  const WorkItemChildSummary({
    required this.id,
    required this.number,
    required this.title,
    required this.statusId,
  });

  factory WorkItemChildSummary.fromJson(Map<String, dynamic> json) =>
      WorkItemChildSummary(
        id: json['id'] as String,
        number: json['number'] as int,
        title: json['title'] as String,
        statusId: json['statusId'] as String,
      );

  final String id;
  final int number;
  final String title;
  final String statusId;
}
