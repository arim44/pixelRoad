import { Module } from '@nestjs/common';
import { AiService } from './ai.service';
import { AiController } from './ai.controller';
import { AiReportService } from './ai-report.service';
import { LandmarkModule } from '../landmark/landmark.module';

@Module({
  imports: [LandmarkModule],
  controllers: [AiController],
  providers: [AiService, AiReportService],
})
export class AiModule {}
