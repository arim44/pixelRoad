
// AI report 분석 생성

import { Injectable } from "@nestjs/common";
import { LandmarkService } from "../landmark/landmark.service";
import { AiReportResponseDto, CreateReportDto, VisitedLandmarkDto } from "./dto/aiReport.dto";
import { Landmark } from "../landmark/landmark.interface";

// 방문 랜드마크등의 결과를 open AI 에게 전달하고 분석 결과 반환
// 실제 리포트 생성 로직
@Injectable()
export class AiReportService {
    constructor(private readonly landmarkService: LandmarkService) { }

    async analysis(visitedLandmarks: VisitedLandmarkDto[]) {
        // 1 방문 데이터 받기

        // 2 전체 랜드마크 가져오기
        const landmarks: Landmark[] = [];

        // 방문한 랜드마크 ID 추출
        // const visitedLandmarkIds = visitedLandmarks.map(
        //     (landmark) => landmark.landmarkId);

        // 방문한 랜드마크 조회
        for (const visitedLandmark of visitedLandmarks) {
            const landmark = await this.landmarkService.getLandmarkById(
                visitedLandmark.landmarkId);

            if (landmark) {
                landmarks.push(landmark);
            }
        }

        // 미방문 랜드마크만 필터링

        // 추천 랜드마크 1개 선정
        // Prompt 생성
        // Gemma 호출
        // 결과 가공

        return landmarks;
        // {
        //     message: 'AI 탐험 리포트 생성 API',
        // };
    }

    // async analysis(dto: CreateReportDto): Promise<AiReportResponseDto>{
    //     //OpenAI에게 전달할 메시지 생성
    //     const message: 
    // }
    // retrun ;

}