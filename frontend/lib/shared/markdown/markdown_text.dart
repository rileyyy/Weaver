import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_markdown_plus/flutter_markdown_plus.dart';
import 'package:weaver/shared/markdown/task_checkbox.dart';
import 'package:weaver/shared/markdown/task_list.dart';
import 'package:weaver/shared/markdown/task_toggle_scope.dart';
import 'package:weaver/shared/navigation/external_link_opener.dart';

/// Renders user-written Markdown (descriptions, comments). Raw HTML is shown
/// as text, images show their alt text instead of being fetched, and only
/// web and mail links open.
///
/// With [onToggleTask], task-list checkboxes can be ticked; it receives the
/// task's index as counted by [toggleTask].
class MarkdownText extends StatelessWidget {
  const MarkdownText(this.data, {this.onToggleTask, this.openLink, super.key});

  final String data;
  final ValueChanged<int>? onToggleTask;

  /// Defaults to opening the link outside the app. Tests pass one in.
  final LinkOpener? openLink;

  static const Set<String> _openableSchemes = {'http', 'https', 'mailto'};

  @override
  Widget build(BuildContext context) {
    final canToggle = onToggleTask != null && taskLinesMatchRenderer(data);
    return TaskToggleScope(
      onToggle: canToggle ? onToggleTask : null,
      child: MarkdownBody(
        data: data,
        extensionSet: markdownExtensions,
        checkboxBuilder: (value) => TaskCheckbox(value: value),
        // An image would be fetched from wherever the author pointed it,
        // which on native apps leaks the reader's address to that server.
        imageBuilder: (uri, title, alt) => Text(alt ?? uri.toString()),
        onTapLink: (text, href, title) => _open(href),
      ),
    );
  }

  void _open(String? href) {
    final uri = Uri.tryParse(href ?? '');
    if (uri == null || !_openableSchemes.contains(uri.scheme)) return;
    unawaited((openLink ?? openExternalLink)(uri));
  }
}
