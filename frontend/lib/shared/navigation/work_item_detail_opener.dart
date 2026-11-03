import 'package:flutter/widgets.dart';

/// Opens a work item's detail dialog and completes with whether it changed
/// something the shared `WorkItemStore` doesn't carry (e.g. repeat settings
/// that create items), so the caller should reload. Edits to the item
/// itself need no reload. Passed in by the app's composition root so
/// features that show work items don't import the detail feature directly.
typedef WorkItemDetailOpener =
    Future<bool> Function(
      BuildContext context, {
      required String workItemId,
      VoidCallback? onDrillInto,
    });
