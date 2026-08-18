// 요청/응답 형식
import { ApiProperty } from "@nestjs/swagger";
import { Type } from "class-transformer";
import { IsArray, IsInt, IsNumber, Min, ValidateNested } from "class-validator";

// 요청 DTO 프론트에서 API로 보내는 데이터
export class VisitedLandmarkDto {
    @ApiProperty({ example: 2, description: "랜드마크 ID" })
    @IsInt()
    landmarkId: number;

    @ApiProperty({ example: 2, description: "총 방문 횟수" })
    @IsInt()
    @Min(1)
    visitCount: number;

}

// 리포트 생성 DTO
export class CreateReportDto {
    @ApiProperty({
        type: [VisitedLandmarkDto],
        description: "사용자의 방문 랜드마크 목록"
    })
    @IsArray()
    @ValidateNested({each: true})
    @Type(()=> VisitedLandmarkDto)
    visitedLandmarks: VisitedLandmarkDto[];
}

// 추천 랜드마크 DTO
export class RecommendationDto{
    @ApiProperty({ example: 24, description: "추천 랜드마크 ID" })
    landmarkId: number;

    @ApiProperty({ example: "창덕궁", description: "추천 랜드마크 이름" })
    name: string;

    @ApiProperty({
        example: "조선 시대의 궁궐을 더 탐험해보세요", 
        description: "추천 이유"
    })
    reason: string;
}

// AI에게 전달할 방문 랜드마크 데이터
// 백엔드가 랜드마크 정보를 붙여 AI에게 전달하는 데이터
export class VisitedLandmarkAiDto {
    @ApiProperty({ example: 24, description: "추천 랜드마크 ID" })
    landmarkId: number;

    @ApiProperty({ example: "창덕궁", description: "추천 랜드마크 이름" })
    name: string;

    @ApiProperty({ example: "역사", description: "추천 랜드마크 카테고리" })
    category: string;

    @ApiProperty({ example: 2, description: "총 방문 횟수" })
    @IsInt()
    @Min(1)
    visitCount: number;
}

// AI 리포트 결과 DTO
export class AiReportResponseDto {
    @ApiProperty({
        example: "궁궐과 역사 유적을 중심으로 탐험하는 성향입니다",
        description: "사용자의 탐험 성향분석"
    })
    analysis: string;

    @ApiProperty({
        type: RecommendationDto,
        description: "AI가 추천하는 랜드마크"
    })
    recommendation: RecommendationDto;
   
}

// API 응답용
export class AiReportApiDto {
    @ApiProperty({
        example: true,
        description: "성공 여부"
    })
    success: boolean;

    @ApiProperty({ type: AiReportResponseDto })
    data: AiReportResponseDto;
}