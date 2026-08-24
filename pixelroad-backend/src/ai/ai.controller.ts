import { Controller, Post, Body, HttpCode, HttpStatus } from '@nestjs/common';
import { AiService } from './ai.service';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { AiReportApiDto, CreateReportDto } from './dto/aiReport.dto';

@ApiTags('AI 탐험 리포트')
@Controller('api/ai')
export class AiController {
  constructor(private readonly aiService: AiService) {}
 
  /**
    * 사용자의 방문 기록을 기반으로
    * AI 탐험 리포트를 생성합니다.
    */
  // POST - /api/ai/report
  @Post('report')
  @HttpCode(HttpStatus.OK)
  @ApiOperation({summary: 'AI 탐험 리포트 생성',
    description: '사용자의 방문 기록을 기반으로 AI탐험 리포트를 생성합니다'
  })
  @ApiResponse({status: HttpStatus.OK, description: 'AI 탐험 리포트 생성 성공',
    type: AiReportApiDto })
  @ApiResponse({status: HttpStatus.BAD_REQUEST, 
    description: '잘못된 요청 데이터' })
  @ApiResponse({status: HttpStatus.INTERNAL_SERVER_ERROR,
    description: '서버 오류 또는 AI 호출 실패' })
  async createReport(@Body() dto:CreateReportDto) { 
    return this.aiService.createReport(dto);
  }
}
