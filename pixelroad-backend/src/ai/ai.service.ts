import { Injectable } from '@nestjs/common';
import { CreateReportDto } from './dto/aiReport.dto';
import { AiReportService } from './ai-report.service';
import { AiClientService } from './ai-client.service';

@Injectable()
export class AiService {
  constructor(private readonly aireportService: AiReportService,
    private readonly aiClientService: AiClientService) { }

  // AI 분석 생성(리포트 전체 흐름 관리)
  async createReport(dto: CreateReportDto) {
    // 1 방문 기록 분석 + 추천 랜드마크 선정 + Prompt 생성
    const result = await this.aireportService.getReportData(dto.visitedLandmarks);
    // 2 AI API 호출
    const aiResult = await this.aiClientService.generateReport(
      result.prompt);

    // 3 최종 API 응답 생성
    return {
      success: true,
      data: {
        analysis: aiResult.analysis,

        recommendation: result.recommendation ? {
          landmarkId: result.recommendation?.id ?? 0,
          name: result.recommendation?.name ?? '',
          reason: aiResult.reason,
        } : {
          landmarkId: 0,
          name: "",
          reason: "현재 추천 가능한 랜드마크가 없습니다."
        }
      }
    };
  }
}
