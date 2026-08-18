import { Injectable } from '@nestjs/common';
import { CreateReportDto } from './dto/aiReport.dto';
import { AiReportService } from './ai-report.service';
import { LandmarkService } from '../landmark/landmark.service';

@Injectable()
export class AiService {
  constructor(private readonly aireportService: AiReportService,
    private readonly landmarkService: LandmarkService,) {}

  // AI 분석 생성(리포트 전체 흐름 관리)
   async createReport(dto: CreateReportDto) {
    // aireportService 호출
    const result = await this.aireportService.analysis(dto.visitedLandmarks);

    // AI 결과 반환
    return { success: true, data: {
      analysis: "현재까지 방문한 랜드마크를 분석하고 있습니다.",
      recommendation: {
        landmarkId: result[0]?.id ?? 0,
        name: result[0]?.name ?? '',
        reason: "방문 기록을 기반으로 추천장소를 준비하고 있습니다.",
      }
    } };      

    // const result = await this.aireportService.analysis(visitedLandmarkIds);
    // return { success: true, data: '결과' };
  }
  // async createReport(dto: CreateReportDto) {
  //   const result = await this.aireportService.analysis();
  //   return { success: true, data: '결과' };
  // }

}
