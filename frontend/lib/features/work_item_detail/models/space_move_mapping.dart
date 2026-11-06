/// Where one status, Type or priority used by the moving items goes in the
/// target Space by default (`SpaceMoveMappingDto`). A null [targetId] means
/// a Type or priority that would be cleared. [itemCount] is how many of the
/// moving items use it.
class SpaceMoveMapping {
  const SpaceMoveMapping({
    required this.sourceId,
    required this.sourceName,
    required this.targetId,
    required this.itemCount,
  });

  factory SpaceMoveMapping.fromJson(Map<String, dynamic> json) =>
      SpaceMoveMapping(
        sourceId: json['sourceId'] as String,
        sourceName: json['sourceName'] as String,
        targetId: json['targetId'] as String?,
        itemCount: json['itemCount'] as int,
      );

  final String sourceId;
  final String sourceName;
  final String? targetId;
  final int itemCount;
}
