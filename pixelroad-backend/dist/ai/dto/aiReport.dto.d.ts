export declare class VisitedLandmarkDto {
    landmarkId: number;
    visitCount: number;
}
export declare class CreateReportDto {
    visitedLandmarks: VisitedLandmarkDto[];
}
export declare class RecommendationDto {
    landmarkId: number;
    name: string;
    reason: string;
}
export declare class VisitedLandmarkAiDto {
    landmarkId: number;
    name: string;
    category: string;
    visitCount: number;
}
export declare class AiReportResponseDto {
    analysis: string;
    recommendation: RecommendationDto;
}
export declare class AiReportApiDto {
    success: boolean;
    data: AiReportResponseDto;
}
