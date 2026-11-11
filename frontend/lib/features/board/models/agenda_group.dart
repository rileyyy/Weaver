import 'package:weaver/shared/models/work_item.dart';

/// One heading of the Agenda and the items under it: Overdue when [day] is
/// null, else that calendar day.
class AgendaGroup {
  const AgendaGroup({required this.day, required this.items});

  final DateTime? day;
  final List<WorkItem> items;

  bool get isOverdue => day == null;
}
