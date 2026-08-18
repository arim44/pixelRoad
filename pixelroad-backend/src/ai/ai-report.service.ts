
// AI report 분석 생성

import { Injectable, NotFoundException } from "@nestjs/common";
import { LandmarkService } from "../landmark/landmark.service";
import { AiReportResponseDto, CreateReportDto, VisitedLandmarkAiDto, VisitedLandmarkDto } from "./dto/aiReport.dto";
import { LandmarkRecommendationService } from "./landmark-recommendation.service";
import { AI_REPORT_SYSTEM_PROMPTS, createAiReportPrompt } from "./constants/ai-report-prompt.constant";

// 방문 랜드마크등의 결과를 open AI 에게 전달하고 분석 결과 반환
// 실제 리포트 생성 로직
@Injectable()
export class AiReportService {
    constructor(private readonly landmarkService: LandmarkService,
        private readonly recommendationService: LandmarkRecommendationService,
    ) { }

    async getReportData(visitedLandmarks: VisitedLandmarkDto[]) {

        // // 1 방문 데이터 받기        
        // const landmarks: Landmark[] = [];

        // // 방문한 랜드마크 조회
        // for (const visitedLandmark of visitedLandmarks) {
        //     const landmark = await this.landmarkService.getLandmarkById(
        //         visitedLandmark.landmarkId);

        //     if (landmark) {
        //         landmarks.push(landmark);
        //     }
        // }

        // AI에 넘길 데이터
        const visited: VisitedLandmarkAiDto[] = [];

        for (const visitedLandmark of visitedLandmarks) {
            const landmark = await this.landmarkService.getLandmarkById(
                visitedLandmark.landmarkId
            );
            if (landmark) {
                visited.push({
                    landmarkId: landmark.id,
                    name: landmark.name,
                    category: landmark.category,
                    visitCount: visitedLandmark.visitCount,
                });
            }
        }


        // 1 전체 랜드마크 조회
        const allLandmarks = await this.landmarkService.getAllLandmarks();

        // 2 추천 랜드마크 1개 선정
        const recommendation = this.recommendationService.selectRecommendation(
            visitedLandmarks, allLandmarks);


        // 3 AI Prompt 생성
        const prompt = createAiReportPrompt(visited, recommendation);
        // console.log("AI Promt: ", prompt);

        // Gemma 호출
        // 결과 가공

        if (!recommendation) {
            return {
                recommendation: undefined,
                systemPrompt: AI_REPORT_SYSTEM_PROMPTS.AIREPORT_ANALYSIS,
                prompt: createAiReportPrompt(
                    visited,
                    undefined
                ),
            };
        }

        return {
            // visited,
            // unvisited,
            recommendation,
            systemPrompt: AI_REPORT_SYSTEM_PROMPTS.AIREPORT_ANALYSIS,
            prompt,
        };
    }
}