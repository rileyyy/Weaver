import 'package:weaver/shared/models/work_item.dart';

/// The server-side work-item query (ADR-0006), as pickers use it: the first
/// page of matches, in Number order.
abstract class WorkItemSearchRepository {
  /// Items in [spaceId] (every Space when null) whose title, description or
  /// When matches [text]; `#12` finds item #12. Blank [text] matches
  /// everything.
  Future<List<WorkItem>> search({String? spaceId, required String text});
}
