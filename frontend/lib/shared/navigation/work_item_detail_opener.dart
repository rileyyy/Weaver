import 'package:flutter/widgets.dart';

/// Opens a work item's detail dialog and completes with whether anything in
/// it changed. Passed in by the app's composition root so features that
/// show work items don't import the detail feature directly.
typedef WorkItemDetailOpener =
    Future<bool> Function(
      BuildContext context, {
      required String workItemId,
      VoidCallback? onDrillInto,
    });
