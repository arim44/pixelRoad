import { Controller, Get, Post, Body, Param } from '@nestjs/common';
import { AiService } from './ai.service';
import { ApiOperation, ApiResponse, ApiTags } from '@nestjs/swagger';
import { AiReportApiDto, CreateReportDto } from './dto/aiReport.dto';

@ApiTags('AI 탐험 리포트')
@Controller('api/ai')
export class AiController {
  constructor(private readonly aiService: AiService) {}
 
  // POST - /api/ai/report
  @Post('report')
  @ApiOperation({summary: 'AI 탐험 리포트 생성',
    description: '사용자의 방문 기록을 기반으로 AI탐험 리포트를 생성합니다'
  })
  @ApiResponse({status: 201, description: 'AI탐험리포트 분석 결과',
    type: AiReportApiDto })
  async createReport(@Body() dto:CreateReportDto) { // : Promise<AiReportApiDto> 
    return this.aiService.createReport(dto);
  }

  // create(@Body() createAiDto: CreateAiDto) {
  //   return this.aiService.create(createAiDto);
  // }

  // @Get()
  // findAll() {
  //   return this.aiService.findAll();
  // }

  // @Get(':id')
  // findOne(@Param('id') id: string) {
  //   return this.aiService.findOne(+id);
  // }

  // @Patch(':id')
  // update(@Param('id') id: string, @Body() updateAiDto: UpdateAiDto) {
  //   return this.aiService.update(+id, updateAiDto);
  // }

  // @Delete(':id')
  // remove(@Param('id') id: string) {
  //   return this.aiService.remove(+id);
  // }
}
