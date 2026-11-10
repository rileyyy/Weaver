import 'package:weaver/features/board/models/view_layout.dart';
import 'package:weaver/shared/models/tag_match.dart';

/// Everything a saved View stores: its name, the query inputs it keeps and
/// its layout. A View keeps no time range; the header's range applies to
/// whichever View is open.
class ViewDefinition {
  const ViewDefinition({
    required this.name,
    this.spaceIds = const {},
    this.text = '',
    this.tags = const {},
    this.tagMatch = TagMatch.any,
    this.applyDoneHiding = true,
    this.scheduledOrInProgressOnly = false,
    this.layout = ViewLayout.board,
  });

  factory ViewDefinition.fromJson(Map<String, dynamic> json) => ViewDefinition(
    name: json['name'] as String,
    spaceIds: {
      for (final id in json['spaceIds'] as List<dynamic>) id as String,
    },
    text: json['text'] as String? ?? '',
    tags: {for (final tag in json['tags'] as List<dynamic>) tag as String},
    tagMatch: TagMatch.fromWire(json['tagMatch'] as String?),
    applyDoneHiding: json['applyDoneHiding'] as bool,
    scheduledOrInProgressOnly: json['scheduledOrInProgressOnly'] as bool,
    layout: ViewLayout.fromWire(json['layout'] as String?),
  );

  final String name;

  /// Empty for every Space, including ones created later.
  final Set<String> spaceIds;
  final String text;
  final Set<String> tags;
  final TagMatch tagMatch;

  /// False shows Done items however long ago they were completed.
  final bool applyDoneHiding;

  /// Overview's rule: only items in a Doing status or with dates.
  final bool scheduledOrInProgressOnly;
  final ViewLayout layout;

  ViewDefinition renamed(String name) => ViewDefinition(
    name: name,
    spaceIds: spaceIds,
    text: text,
    tags: tags,
    tagMatch: tagMatch,
    applyDoneHiding: applyDoneHiding,
    scheduledOrInProgressOnly: scheduledOrInProgressOnly,
    layout: layout,
  );

  Map<String, Object?> toJson() => {
    'name': name,
    'spaceIds': spaceIds.toList(),
    'text': text.isEmpty ? null : text,
    'tags': tags.toList(),
    'tagMatch': tagMatch.wireName,
    'applyDoneHiding': applyDoneHiding,
    'scheduledOrInProgressOnly': scheduledOrInProgressOnly,
    'layout': layout.wireName,
  };
}
