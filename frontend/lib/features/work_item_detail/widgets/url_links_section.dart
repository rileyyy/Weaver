import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_url_link.dart';
import 'package:weaver/shared/navigation/external_link_opener.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// This item's links to things outside Weaver, such as PRs and quotes.
class UrlLinksSection extends StatefulWidget {
  const UrlLinksSection({
    super.key,
    required this.links,
    required this.onAdd,
    required this.onRemove,
    this.openLink,
  });

  final List<WorkItemUrlLink> links;

  /// Called with the trimmed, non-empty URL and the label as typed (maybe
  /// blank); returns whether the link was added, which clears the fields.
  final Future<bool> Function(String url, String label) onAdd;

  /// Called with the URL link's id.
  final ValueChanged<String> onRemove;

  /// Defaults to opening the link outside the app. Tests pass one in.
  final LinkOpener? openLink;

  @override
  State<UrlLinksSection> createState() => _UrlLinksSectionState();
}

class _UrlLinksSectionState extends State<UrlLinksSection> {
  final TextEditingController _url = TextEditingController();
  final TextEditingController _label = TextEditingController();

  @override
  void dispose() {
    _url.dispose();
    _label.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final url = _url.text.trim();
    if (url.isEmpty) return;
    final added = await widget.onAdd(url, _label.text);
    if (!added || !mounted) return;
    _url.clear();
    _label.clear();
  }

  void _open(WorkItemUrlLink link) {
    final uri = Uri.tryParse(link.url);
    if (uri == null) return;
    unawaited((widget.openLink ?? openExternalLink)(uri));
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('URL Links'),
        for (final link in widget.links) _linkTile(link),
        TextField(
          controller: _url,
          keyboardType: TextInputType.url,
          decoration: const InputDecoration(hintText: 'https://…'),
          onSubmitted: (_) => unawaited(_submit()),
        ),
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _label,
                decoration: const InputDecoration(hintText: 'Label (optional)'),
                onSubmitted: (_) => unawaited(_submit()),
              ),
            ),
            const SizedBox(width: 8),
            TextButton(
              onPressed: () => unawaited(_submit()),
              child: const Text('Add URL link'),
            ),
          ],
        ),
      ],
    );
  }

  Widget _linkTile(WorkItemUrlLink link) => ListTile(
    contentPadding: EdgeInsets.zero,
    dense: true,
    leading: const Icon(Icons.open_in_new, size: 18),
    title: Text(link.displayName, overflow: TextOverflow.ellipsis),
    subtitle: link.label == null
        ? null
        : Text(link.url, overflow: TextOverflow.ellipsis),
    onTap: () => _open(link),
    trailing: IconButton(
      icon: const Icon(Icons.link_off, size: 18),
      tooltip: 'Remove URL link',
      onPressed: () => widget.onRemove(link.id),
    ),
  );
}
